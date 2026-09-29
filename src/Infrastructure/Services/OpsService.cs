using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Application.Printing;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

// ═══════════════════════════ Cameras ═══════════════════════════
public class CameraService : ICameraService
{
    private readonly AppDbContext _db;
    public CameraService(AppDbContext db) => _db = db;

    public async Task<List<CameraDto>> GetAllAsync(IUserContext user)
    {
        var q = _db.Cameras.AsNoTracking().Where(c => c.IsActive);
        if (!user.IsSuperAdmin && user.BranchId != null) q = q.Where(c => c.BranchId == user.BranchId);
        var list = await q.OrderBy(c => c.Name).ToListAsync();
        return list.Select(c => new CameraDto { Id = c.Id, Name = c.Name, Type = c.Type, Url = c.Url, Location = c.Location, IsActive = c.IsActive, BranchId = c.BranchId }).ToList();
    }

    public async Task<CameraDto> UpsertAsync(CameraUpsertDto dto)
    {
        Camera c;
        if (dto.Id is null) { c = new Camera(); _db.Cameras.Add(c); }
        else c = await _db.Cameras.FindAsync(dto.Id) ?? throw AppException.NotFound("Camera");
        c.Name = dto.Name; c.Type = dto.Type; c.Url = dto.Url; c.Location = dto.Location; c.IsActive = dto.IsActive; c.BranchId = dto.BranchId;
        await _db.SaveChangesAsync();
        return new CameraDto { Id = c.Id, Name = c.Name, Type = c.Type, Url = c.Url, Location = c.Location, IsActive = c.IsActive, BranchId = c.BranchId };
    }

    public async Task DeleteAsync(Guid id)
    {
        var c = await _db.Cameras.FindAsync(id) ?? throw AppException.NotFound("Camera");
        c.IsDeleted = true; c.IsActive = false;
        await _db.SaveChangesAsync();
    }

    public Task<Camera?> FindAsync(Guid id) => _db.Cameras.FirstOrDefaultAsync(c => c.Id == id);
}

// ═══════════════════════════ ESC/POS Printing ═══════════════════════════
public class PrintService : IPrintService
{
    private readonly AppDbContext _db;
    private readonly ISettingsService _settings;
    private readonly ILogger<PrintService> _log;

    public PrintService(AppDbContext db, ISettingsService settings, ILogger<PrintService> log) => (_db, _settings, _log) = (db, settings, log);

    public async Task PrintKitchenTicketAsync(Guid orderId)
    {
        var order = await _db.Orders.Include(o => o.Items).Include(o => o.Table).Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;
        var branch = await _db.Branches.FindAsync(order.BranchId);
        var printer = await _db.Printers.FirstOrDefaultAsync(p => p.BranchId == order.BranchId && p.Type == PrinterType.Kitchen && !p.IsDeleted)
            ?? await _db.Printers.FirstOrDefaultAsync(p => p.Type == PrinterType.Kitchen && !p.IsDeleted);
        if (printer == null) { _log.LogWarning("No kitchen printer configured — ticket for {Order} skipped (visible in KDS)", order.OrderNumber); return; }

        var ticket = new KitchenTicket
        {
            OrderNumber = order.OrderNumber,
            TableOrType = order.Table != null ? $"Table {order.Table.Number}" : order.Type.ToString(),
            CreatedAt = order.CreatedAt, Customer = order.CustomerName, Note = order.Note,
            Lines = order.Items.Where(i => i.Status != OrderItemStatus.Cancelled).Select(i => new KitchenTicketLine { Name = i.ItemNameSnapshot, Quantity = i.Quantity, Notes = i.Notes }).ToList()
        };
        await SendAsync(printer, order.BranchId, order.Id, ticket);
    }

    public async Task PrintReceiptAsync(Guid orderId, Guid? paymentId)
    {
        var order = await _db.Orders.Include(o => o.Items).Include(o => o.Table).Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;
        var branch = await _db.Branches.FindAsync(order.BranchId);
        var printer = await _db.Printers.FirstOrDefaultAsync(p => p.BranchId == order.BranchId && p.Type == PrinterType.Receipt && !p.IsDeleted)
            ?? await _db.Printers.FirstOrDefaultAsync(p => p.Type == PrinterType.Receipt && !p.IsDeleted);
        if (printer == null) { _log.LogWarning("No receipt printer configured — receipt for {Order} skipped", order.OrderNumber); return; }

        var payment = paymentId != null ? order.Payments.FirstOrDefault(p => p.Id == paymentId) : order.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault(p => p.Status == PaymentStatus.Paid);
        var receipt = new ReceiptData
        {
            RestaurantName = await _settings.GetAsync("Restaurant:Name", "Zafaran Restaurant"),
            BranchName = branch?.Name ?? "-", OrderNumber = order.OrderNumber, CompletedAt = order.CompletedAt ?? DateTime.UtcNow,
            Cashier = order.CreatedByName, Footer = await _settings.GetAsync("Receipt:Footer", "نوش جان — با تشکر از انتخاب شما"),
            SubTotal = order.SubTotal, Discount = order.Discount, Tax = order.Tax, Total = order.Total,
            PaymentMethod = payment?.Gateway.ToString() ?? "Unpaid", RefId = payment?.RefId,
            Lines = order.Items.Select(i => new ReceiptLine { Name = i.ItemNameSnapshot, Quantity = i.Quantity, UnitPrice = i.UnitPrice }).ToList()
        };
        await SendAsync(printer, order.BranchId, order.Id, receipt);
    }

    public async Task<bool> TestPrinterAsync(Guid printerId)
    {
        var printer = await _db.Printers.FindAsync(printerId) ?? throw AppException.NotFound("Printer");
        var ticket = new KitchenTicket { OrderNumber = "TEST", TableOrType = "Printer test", CreatedAt = DateTime.UtcNow, Lines = new() { new KitchenTicketLine { Name = "ESC/POS test — چاپ آزمایشی", Quantity = 1 } } };
        return await SendAsync(printer, printer.BranchId, null, ticket);
    }

    public async Task<List<PrinterDto>> GetPrintersAsync(Guid? branchId)
    {
        var q = _db.Printers.AsNoTracking().Where(p => !p.IsDeleted);
        if (branchId != null) q = q.Where(p => p.BranchId == branchId);
        var list = await q.OrderBy(p => p.Name).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<PrinterDto> UpsertAsync(PrinterDto dto)
    {
        PrinterConfig p;
        if (dto.Id == Guid.Empty) { p = new PrinterConfig(); _db.Printers.Add(p); }
        else p = await _db.Printers.FindAsync(dto.Id) ?? throw AppException.NotFound("Printer");
        p.Name = dto.Name; p.Type = dto.Type; p.Host = dto.Host; p.Port = dto.Port; p.IsDefault = dto.IsDefault; p.UseRasterMode = dto.UseRasterMode; p.Encoding = dto.Encoding; p.BranchId = dto.BranchId ?? p.BranchId;
        await _db.SaveChangesAsync();
        return ToDto(p);
    }

    public async Task DeletePrinterAsync(Guid id)
    {
        var p = await _db.Printers.FindAsync(id) ?? throw AppException.NotFound("Printer");
        p.IsDeleted = true;
        await _db.SaveChangesAsync();
    }

    // ── core: build bytes (raster Persian-safe OR text), TCP 9100, log job ──
    private async Task<bool> SendAsync(PrinterConfig printer, Guid? branchId, Guid? orderId, object model)
    {
        byte[] bytes;
        string plain;
        try
        {
            if (printer.UseRasterMode)
            {
                var (raster, w, h) = SkiaReceiptRenderer.RenderToRaster(model);
                bytes = EscPosEncoder.Init().Concat(EscPosEncoder.RasterImage(raster, w, h)).Concat(EscPosEncoder.Feed(6)).Concat(EscPosEncoder.Cut()).ToArray();
                plain = $"[raster {w}x{h}] {model.GetType().Name}";
            }
            else
            {
                bytes = model switch
                {
                    KitchenTicket t => EscPosEncoder.EncodeKitchenTicket(t, printer.Encoding),
                    ReceiptData r => EscPosEncoder.EncodeReceipt(r, printer.Encoding),
                    _ => throw new ArgumentException("model")
                };
                plain = "[text mode]";
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to encode print job for {Printer}", printer.Name);
            _db.PrintJobs.Add(new PrintJob { PrinterId = printer.Id, OrderId = orderId, PrinterType = printer.Type, Content = "", PlainText = "encode failed: " + ex.Message, Success = false, Error = ex.Message, BranchId = branchId });
            await _db.SaveChangesAsync();
            return false;
        }

        var job = new PrintJob { PrinterId = printer.Id, OrderId = orderId, PrinterType = printer.Type, Content = Convert.ToBase64String(bytes), PlainText = plain, Attempts = 1, BranchId = branchId };
        try
        {
            await NetworkPrinterSender.SendAsync(printer.Host, printer.Port, bytes);
            job.Success = true;
            _log.LogInformation("Printed {Type} for printer {Printer} ({Host}:{Port})", printer.Type, printer.Name, printer.Host, printer.Port);
        }
        catch (Exception ex)
        {
            job.Success = false; job.Error = ex.Message;
            _log.LogWarning(ex, "Print failed → {Printer} ({Host}:{Port}). Job logged for retry/debug.", printer.Name, printer.Host, printer.Port);
        }
        _db.PrintJobs.Add(job);
        await _db.SaveChangesAsync();
        return job.Success;
    }

    internal static PrinterDto ToDto(PrinterConfig p) => new()
    { Id = p.Id, Name = p.Name, Type = p.Type, Host = p.Host, Port = p.Port, IsDefault = p.IsDefault, UseRasterMode = p.UseRasterMode, Encoding = p.Encoding, BranchId = p.BranchId };
}

// ═══════════════════════════ Notifications ═══════════════════════════
public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<Hubs.NotificationHub, IAppClient> _hub;
    public NotificationService(AppDbContext db, IHubContext<Hubs.NotificationHub, IAppClient> hub) => (_db, _hub) = (db, hub);

    public async Task<NotificationDto> CreateAsync(NotificationType type, string title, string message, Guid? branchId, UserRole? targetRole = null, Guid? userId = null)
    {
        var n = new Notification { Type = type, Title = title, Message = message, BranchId = branchId, TargetRole = targetRole, UserId = userId };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync();
        var dto = ToDto(n);
        if (userId != null) await _hub.Clients.Group($"user:{userId}").ReceiveNotification(dto);
        else if (targetRole != null && branchId != null) await _hub.Clients.Groups($"role:{(int)targetRole}", $"branch:{branchId}").ReceiveNotification(dto);
        else if (branchId != null) await _hub.Clients.Group($"branch:{branchId}").ReceiveNotification(dto);
        else await _hub.Clients.All.ReceiveNotification(dto);
        return dto;
    }

    public async Task<List<NotificationDto>> GetForUserAsync(IUserContext user, int take = 50)
    {
        var q = _db.Notifications.AsNoTracking().AsQueryable();
        if (user.BranchId != null) q = q.Where(n => n.BranchId == null || n.BranchId == user.BranchId);
        var list = await q.OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync();
        return list.Where(n => n.UserId == null || n.UserId == user.UserId).Select(ToDto).ToList();
    }

    public async Task MarkReadAsync(Guid id)
    {
        var n = await _db.Notifications.FindAsync(id) ?? throw AppException.NotFound("Notification");
        n.IsRead = true; await _db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(IUserContext user)
    {
        await _db.Notifications.Where(n => !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    internal static NotificationDto ToDto(Notification n) => new()
    { Id = n.Id, Type = n.Type, Title = n.Title, Message = n.Message, IsRead = n.IsRead, CreatedAt = n.CreatedAt };
}

// ═══════════════════════════ Settings ═══════════════════════════
public class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;
    public SettingsService(AppDbContext db) => _db = db;

    public Task<List<SettingDto>> GetAllAsync() =>
        _db.Settings.AsNoTracking().OrderBy(s => s.Key).Select(s => new SettingDto { Key = s.Key, Value = s.Value, Description = s.Description }).ToListAsync();

    public async Task SetAsync(string key, string value)
    {
        var s = await _db.Settings.FirstOrDefaultAsync(x => x.Key == key);
        if (s == null) { s = new AppSetting { Key = key, Value = value }; _db.Settings.Add(s); }
        else { s.Value = value; s.UpdatedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync();
    }

    public async Task<string> GetAsync(string key, string fallback) =>
        (await _db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key))?.Value ?? fallback;

    public async Task<decimal> GetTaxRateAsync() =>
        decimal.TryParse(await GetAsync("Restaurant:TaxRatePercent", "9"), System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 9m;
}
