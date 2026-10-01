using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Application.Payments;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly GatewayFactory _gateways;
    private readonly IHubContext<Hubs.NotificationHub, IAppClient> _notify;
    private readonly INotificationService _notifications;
    private readonly IOrderService _orders;
    private readonly ILogger<PaymentService> _log;

    public PaymentService(AppDbContext db, GatewayFactory gateways, IHubContext<Hubs.NotificationHub, IAppClient> notify,
        INotificationService notifications, IOrderService orders, ILogger<PaymentService> log)
        => (_db, _gateways, _notify, _notifications, _orders, _log) = (db, gateways, notify, notifications, orders, log);

    // ───────────── Initiate: cash at counter OR online gateway redirect ─────────────
    public async Task<PaymentResultDto> InitiateAsync(PaymentInitiateDto dto, string apiBaseUrl)
    {
        var order = await _db.Orders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == dto.OrderId)
            ?? throw AppException.NotFound("Order");
        if (order.Status == OrderStatus.Cancelled) throw AppException.BadRequest("سفارش لغو‌شده قابل پرداخت نیست.");
        if (order.PaymentStatus == PaymentStatus.Paid) throw AppException.BadRequest("این سفارش قبلاً پرداخت شده است.");

        // instant methods — no gateway round trip
        if (dto.Gateway is PaymentGatewayType.Cash or PaymentGatewayType.CardPresent)
        {
            var p = new Payment { OrderId = order.Id, Gateway = dto.Gateway, Amount = order.Total, Status = PaymentStatus.Paid, PaidAt = DateTime.UtcNow, GatewayResponse = "{\"manual\":true}" };
            _db.Payments.Add(p);
            order.PaymentStatus = PaymentStatus.Paid;
            await _db.SaveChangesAsync();
            await OnPaidAsync(p, order);
            return new PaymentResultDto { PaymentId = p.Id, IsSandbox = false, RedirectUrl = "", GatewayName = dto.Gateway.ToString() };
        }

        var cfg = await _db.GatewayConfigs.FirstOrDefaultAsync(c => c.Gateway == dto.Gateway && c.Enabled)
            ?? throw AppException.BadRequest($"درگاه {Fa.Label(dto.Gateway)} فعال نیست. با مدیر ارشد سیستم تماس بگیرید.");
        var impl = _gateways.Resolve(dto.Gateway) ?? throw AppException.BadRequest("پیاده‌سازی درگاه پرداخت یافت نشد.");

        var payment = new Payment { OrderId = order.Id, Gateway = dto.Gateway, Amount = order.Total, Status = PaymentStatus.Pending };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        // SANDBOX: internal page that simulates the bank UI then calls back with success
        if (cfg.Sandbox)
        {
            payment.Authority = $"SBX-{payment.Id:N}";
            await _db.SaveChangesAsync();
            _log.LogInformation("Sandbox payment {Payment} started for order {Order}", payment.Id, order.OrderNumber);
            return new PaymentResultDto { PaymentId = payment.Id, IsSandbox = true, RedirectUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/payments/sandbox/{payment.Id}", GatewayName = impl.DisplayName + " (sandbox)" };
        }

        var request = new GatewayRequest
        {
            AmountToman = order.Total, CallbackUrl = $"{apiBaseUrl.TrimEnd('/')}/api/v1/payments/callback/{dto.Gateway}?paymentId={payment.Id}",
            Description = $"Zafaran order {order.OrderNumber}", OrderNumber = order.OrderNumber, Mobile = order.CustomerPhone ?? ""
        };
        var result = await impl.CreateAsync(request, cfg);
        if (!result.Success || string.IsNullOrEmpty(result.RedirectUrl))
        {
            payment.Status = PaymentStatus.Failed; payment.GatewayResponse = result.Raw ?? result.Error;
            await _db.SaveChangesAsync();
            throw AppException.BadRequest($"درگاه پرداخت درخواست را رد کرد: {result.Error}");
        }
        payment.Authority = result.Authority; payment.GatewayResponse = result.Raw;
        await _db.SaveChangesAsync();
        return new PaymentResultDto { PaymentId = payment.Id, IsSandbox = false, RedirectUrl = result.RedirectUrl, GatewayName = impl.DisplayName };
    }

    // ───────────── Bank callback → verify → capture ─────────────
    public async Task<bool> HandleCallbackAsync(PaymentGatewayType gateway, Dictionary<string, string> queryParams)
    {
        var paymentIdStr = queryParams.GetValueOrDefault("paymentId");
        Payment? payment = null;
        if (Guid.TryParse(paymentIdStr, out var pid)) payment = await _db.Payments.Include(p => p.Order).FirstOrDefaultAsync(p => p.Id == pid);
        payment ??= await _db.Payments.Include(p => p.Order).FirstOrDefaultAsync(p => p.Authority != null && (queryParams.ContainsValue(p.Authority)));
        if (payment == null) { _log.LogWarning("Callback for unknown payment. Params: {Params}", string.Join(',', queryParams.Keys)); return false; }

        if (payment.Status == PaymentStatus.Paid) return true;

        var cfg = await _db.GatewayConfigs.FirstOrDefaultAsync(c => c.Gateway == gateway) ?? new PaymentGatewayConfig { Gateway = gateway, Sandbox = true, MerchantKey = null };
        var impl = _gateways.Resolve(gateway);
        if (impl == null) return false;

        var status = queryParams.GetValueOrDefault("Status") ?? queryParams.GetValueOrDefault("status") ?? "OK";
        if (!status.Equals("OK", StringComparison.OrdinalIgnoreCase) && !cfg.Sandbox)
        {
            payment.Status = PaymentStatus.Failed; payment.GatewayResponse = $"callback status={status}";
            await _db.SaveChangesAsync();
            return false;
        }

        var input = new GatewayVerifyInput { Authority = payment.Authority ?? "", AmountToman = payment.Amount, CallbackParams = queryParams };

        GatewayVerifyResult verify;
        if (cfg.Sandbox && (payment.Authority?.StartsWith("SBX-") ?? false))
        {
            // internal sandbox simulator: instant approval, synthetic tracking number
            verify = new GatewayVerifyResult { Success = true, RefId = $"{DateTime.UtcNow:HHmmss}{Random.Shared.Next(1000, 9999)}", CardPanMasked = "6104-****-****-1234", Raw = "{\"sandbox\":true}" };
        }
        else
        {
            verify = await impl.VerifyAsync(input, cfg);
        }
        payment.GatewayResponse = verify.Raw ?? verify.Error;

        if (!verify.Success)
        {
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync();
            _log.LogWarning("Payment {Id} verification failed: {Error}", payment.Id, verify.Error);
            return false;
        }

        payment.Status = PaymentStatus.Paid; payment.PaidAt = DateTime.UtcNow;
        payment.RefId = verify.RefId; payment.CardPanMasked = verify.CardPanMasked;
        payment.Order!.PaymentStatus = PaymentStatus.Paid;
        await _db.SaveChangesAsync();
        await OnPaidAsync(payment, payment.Order);
        return true;
    }

    // Cashier counter shortcuts
    public async Task MarkManualPaidAsync(Guid orderId, PaymentGatewayType method, string? refId, IUserContext actor)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId) ?? throw AppException.NotFound("Order");
        if (order.PaymentStatus == PaymentStatus.Paid) return;
        var p = new Payment { OrderId = order.Id, Gateway = method, Amount = order.Total, Status = PaymentStatus.Paid, PaidAt = DateTime.UtcNow, RefId = refId, GatewayResponse = $"{{\"by\":\"{actor.UserName}\"}}" };
        _db.Payments.Add(p);
        order.PaymentStatus = PaymentStatus.Paid;
        await _db.SaveChangesAsync();
        await OnPaidAsync(p, order);
    }

    public async Task<List<PaymentDto>> ListAsync(Guid? branchId, int take = 100)
    {
        var q = _db.Payments.AsNoTracking().Include(p => p.Order).AsQueryable();
        if (branchId != null) q = q.Where(p => p.BranchId == null || p.BranchId == branchId);
        var list = await q.OrderByDescending(p => p.CreatedAt).Take(take).ToListAsync();
        return list.Select(p => new PaymentDto
        { Id = p.Id, OrderId = p.OrderId, OrderNumber = p.Order?.OrderNumber ?? "?", Gateway = p.Gateway, Amount = p.Amount, Status = p.Status, RefId = p.RefId, CardPanMasked = p.CardPanMasked, PaidAt = p.PaidAt, CreatedAt = p.CreatedAt }).ToList();
    }

    public async Task<List<GatewayConfigDto>> GetGatewayConfigsAsync()
    {
        var list = await _db.GatewayConfigs.OrderBy(c => c.Gateway).ToListAsync();
        return list.Select(c => new GatewayConfigDto { Id = c.Id, Gateway = c.Gateway, Enabled = c.Enabled, Sandbox = c.Sandbox, MerchantKey = Mask(c.MerchantKey) }).ToList();
    }

    public async Task UpdateGatewayConfigAsync(GatewayConfigDto dto)
    {
        var c = await _db.GatewayConfigs.FindAsync(dto.Id) ?? throw AppException.NotFound("Gateway config");
        c.Enabled = dto.Enabled; c.Sandbox = dto.Sandbox;
        if (!string.IsNullOrWhiteSpace(dto.MerchantKey) && !dto.MerchantKey.Contains('•')) c.MerchantKey = dto.MerchantKey.Trim();
        await _db.SaveChangesAsync();
    }

    // ── shared post-payment pipeline ──
    private async Task OnPaidAsync(Payment p, Order order)
    {
        _log.LogInformation("Payment {Payment} captured for order {Order} via {Gateway}", p.Id, order.OrderNumber, p.Gateway);
        await _notifications.CreateAsync(NotificationType.Payment, $"Payment received — {order.OrderNumber}",
            $"{p.Amount:N0} via {p.Gateway}{(p.RefId != null ? $" (Ref {p.RefId})" : "")}", order.BranchId, UserRole.Cashier);

        var dto = new PaymentDto { Id = p.Id, OrderId = p.OrderId, OrderNumber = order.OrderNumber, Gateway = p.Gateway, Amount = p.Amount, Status = p.Status, RefId = p.RefId, PaidAt = p.PaidAt, CreatedAt = p.CreatedAt };
        await _notify.Clients.Groups($"branch:{order.BranchId}").PaymentCaptured(dto);
        await _notify.Clients.Groups($"branch:{order.BranchId}").StatsRefresh("payment");

        // online orders go straight to the kitchen once paid
        if (order.Type != OrderType.DineIn && order.Status == OrderStatus.Pending)
            await _orders.UpdateStatusAsync(order.Id, new OrderStatusUpdateDto { Status = OrderStatus.Confirmed }, new SystemUserContext());
    }

    private static string? Mask(string? key) =>
        string.IsNullOrWhiteSpace(key) ? key : key.Length <= 8 ? "••••" : key[..4] + "••••" + key[^4..];
}

/// <summary>System actor for pipeline-internal calls (auto-confirm after payment).</summary>
public class SystemUserContext : IUserContext
{
    public Guid? UserId => null;
    public string? UserName => "system";
    public UserRole? Role => UserRole.SuperAdmin;
    public Guid? BranchId => null;
    public bool IsSuperAdmin => true;
    public bool IsAuthenticated => true;
}
