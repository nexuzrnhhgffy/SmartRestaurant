using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Customers;
using SmartRestaurant.Domain.Entities.Orders;
using SmartRestaurant.Domain.Entities.Settings;
using SmartRestaurant.Domain.Entities.Tables;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;

    public OrderService(AppDbContext db, INotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, int? userId)
    {
        var settings = await _db.Settings.AsNoTracking().FirstAsync();
        var type = (OrderType)dto.Type;

        var menuIds = dto.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menu = await _db.MenuItems.Where(m => menuIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);

        var order = new Order
        {
            Type = type,
            CreatedById = userId,
            GuestName = dto.GuestName,
            Phone = dto.Phone,
            DeliveryAddress = dto.DeliveryAddress,
            CouponCode = string.IsNullOrWhiteSpace(dto.CouponCode) ? null : dto.CouponCode.Trim().ToUpper(),
            Notes = dto.Notes,
            OrderNumber = await NextOrderNumberAsync(),
            CreatedAt = DateTime.UtcNow
        };

        if (type == OrderType.DineIn && dto.TableId.HasValue)
        {
            order.TableId = dto.TableId;
            var table = await _db.Tables.FindAsync(dto.TableId);
            if (table != null) table.Status = TableStatus.Occupied;
        }

        // Customer linkage (walk-in phone match or logged-in customer)
        if (!dto.CustomerId.HasValue && !string.IsNullOrWhiteSpace(dto.Phone))
        {
            var phone = dto.Phone.Trim();
            order.CustomerId = await _db.Customers.Where(c => c.Phone == phone).Select(c => (int?)c.Id).FirstOrDefaultAsync();
        }
        else order.CustomerId = dto.CustomerId;

        decimal subtotal = 0;
        foreach (var line in dto.Items)
        {
            if (!menu.TryGetValue(line.MenuItemId, out var mi)) continue;
            var qty = Math.Max(1, line.Quantity);
            var lineTotal = mi.Price * qty;
            order.Items.Add(new OrderItem
            {
                MenuItemId = mi.Id,
                NameSnapshot = mi.Name,
                UnitPrice = mi.Price,
                Quantity = qty,
                LineTotal = lineTotal,
                Notes = line.Notes,
                ModifierText = line.ModifierText,
                Status = LineItemStatus.Pending,
                CostSnapshot = mi.Cost * qty
            });
            subtotal += lineTotal;
            mi.TimesOrdered += qty;
        }

        // Coupon
        decimal discount = 0;
        if (!string.IsNullOrEmpty(order.CouponCode))
        {
            var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == order.CouponCode);
            if (coupon != null && coupon.IsValid && subtotal >= coupon.MinOrderAmount)
            {
                discount = coupon.Type == CouponType.Percent
                    ? Math.Round(subtotal * coupon.Value / 100m, 2)
                    : Math.Min(coupon.Value, subtotal);
                coupon.UsedCount++;
            }
            else order.CouponCode = null;
        }

        var taxable = subtotal - discount;
        var tax = Math.Round(taxable * settings.TaxRate, 2);
        var service = type == OrderType.DineIn ? Math.Round(taxable * settings.ServiceChargeRate, 2) : 0;
        var delivery = type == OrderType.Delivery ? settings.DeliveryFee : 0;

        order.Subtotal = subtotal;
        order.DiscountAmount = discount;
        order.TaxAmount = tax;
        order.ServiceCharge = service;
        order.DeliveryFee = delivery;
        order.Total = subtotal - discount + tax + service + delivery;
        order.EstimatedReadyTime = DateTime.UtcNow.AddMinutes(order.Items.Any() ? order.Items.Max(i => menu[i.MenuItemId].PrepTimeMinutes) + 5 : 20);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        await _notify.PushAsync($"New {type} order", $"{order.OrderNumber} — {order.Items.Sum(i => i.Quantity)} items — ${order.Total:F2}",
            1, type == OrderType.DineIn ? Domain.Entities.Identity.AppRole.Chef : Domain.Entities.Identity.AppRole.Manager, "/kitchen");

        return (await GetByIdAsync(order.Id))!;
    }

    public async Task<OrderDto?> GetByIdAsync(int id)
    {
        var o = await _db.Orders
            .Include(x => x.Table).Include(x => x.Customer).Include(x => x.CreatedBy)
            .Include(x => x.Items).Include(x => x.Payments).ThenInclude(p => p.Cashier)
            .AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return o == null ? null : ToDto(o);
    }

    public async Task<List<OrderDto>> GetAllAsync(int? type = null, int? status = null, DateTime? date = null, string? search = null)
    {
        var q = _db.Orders
            .Include(x => x.Table).Include(x => x.Customer).Include(x => x.CreatedBy)
            .Include(x => x.Items).Include(x => x.Payments).ThenInclude(p => p.Cashier)
            .AsNoTracking();
        if (type.HasValue) q = q.Where(o => (int)o.Type == type);
        if (status.HasValue) q = q.Where(o => (int)o.Status == status);
        if (date.HasValue) q = q.Where(o => o.CreatedAt.Date == date.Value.Date);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(o => o.OrderNumber.Contains(search) || (o.GuestName != null && o.GuestName.Contains(search)) || (o.Phone != null && o.Phone.Contains(search)));
        var list = await q.OrderByDescending(o => o.CreatedAt).Take(300).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<OrderDto> UpdateStatusAsync(int id, int status)
    {
        var o = await _db.Orders.Include(x => x.Table).Include(x => x.Items).FirstAsync(x => x.Id == id);
        var newStatus = (OrderStatus)status;
        o.Status = newStatus;
        o.UpdatedAt = DateTime.UtcNow;

        if (newStatus == OrderStatus.Completed)
        {
            o.PaymentStatus = o.Payments.Any() || o.PaymentStatus == PaymentStatus.Paid ? o.PaymentStatus : PaymentStatus.Unpaid;
            if (o.TableId.HasValue)
            {
                var t = await _db.Tables.FindAsync(o.TableId);
                if (t != null) t.Status = TableStatus.Dirty;
            }
            if (o.CustomerId.HasValue)
            {
                var c = await _db.Customers.FindAsync(o.CustomerId);
                if (c != null)
                {
                    var settings = await _db.Settings.AsNoTracking().FirstAsync();
                    c.OrderCount++;
                    c.TotalSpent += o.Total;
                    c.LoyaltyPoints += (int)(o.Total * settings.LoyaltyPointsPerDollar);
                }
            }
        }
        await _db.SaveChangesAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task<OrderDto> UpdateItemStatusAsync(int orderItemId, int status)
    {
        var item = await _db.OrderItems.Include(i => i.Order).FirstAsync(i => i.Id == orderItemId);
        item.Status = (LineItemStatus)status;
        item.Order!.UpdatedAt = DateTime.UtcNow;

        // auto-advance order status based on item statuses
        var items = await _db.OrderItems.Where(i => i.OrderId == item.OrderId).ToListAsync();
        if (items.All(i => i.Status == LineItemStatus.Ready || i.Status == LineItemStatus.Served || i.Status == LineItemStatus.Cancelled))
            item.Order.Status = OrderStatus.Ready;
        else if (items.Any(i => i.Status == LineItemStatus.Preparing) && item.Order.Status < OrderStatus.Preparing)
            item.Order.Status = OrderStatus.Preparing;

        await _db.SaveChangesAsync();
        return (await GetByIdAsync(item.OrderId))!;
    }

    public async Task<bool> AddItemAsync(int orderId, CreateOrderItemDto dto)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == orderId);
        var mi = await _db.MenuItems.FirstAsync(m => m.Id == dto.MenuItemId);
        order.Items.Add(new OrderItem
        {
            MenuItemId = mi.Id, NameSnapshot = mi.Name, UnitPrice = mi.Price, Quantity = Math.Max(1, dto.Quantity),
            LineTotal = mi.Price * dto.Quantity, Notes = dto.Notes, ModifierText = dto.ModifierText,
            Status = LineItemStatus.Pending, CostSnapshot = mi.Cost * dto.Quantity
        });
        order.Subtotal += mi.Price * dto.Quantity;
        order.Total = order.Subtotal - order.DiscountAmount + order.TaxAmount + order.ServiceCharge + order.DeliveryFee;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveItemAsync(int orderItemId)
    {
        var item = await _db.OrderItems.Include(i => i.Order).FirstOrDefaultAsync(i => i.Id == orderItemId);
        if (item == null) return false;
        var order = item.Order!;
        order.Subtotal -= item.LineTotal;
        order.Total = order.Subtotal - order.DiscountAmount + order.TaxAmount + order.ServiceCharge + order.DeliveryFee;
        _db.OrderItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelAsync(int id, string? reason)
    {
        var o = await _db.Orders.Include(x => x.Table).FirstOrDefaultAsync(x => x.Id == id);
        if (o == null || o.Status == OrderStatus.Completed) return false;
        o.Status = OrderStatus.Cancelled;
        o.Notes = string.IsNullOrWhiteSpace(o.Notes) ? $"Cancelled: {reason}" : o.Notes + $" | Cancelled: {reason}";
        if (o.TableId.HasValue)
        {
            var t = await _db.Tables.FindAsync(o.TableId);
            if (t != null) t.Status = TableStatus.Dirty;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<OrderStatsDto> GetStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var todays = await _db.Orders.Include(o => o.Payments).Where(o => o.CreatedAt.Date == today).AsNoTracking().ToListAsync();
        var completed = todays.Where(o => o.Status == OrderStatus.Completed).ToList();
        return new OrderStatsDto(
            todays.Count(o => o.Status == OrderStatus.Pending),
            todays.Count(o => o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Confirmed),
            todays.Count(o => o.Status == OrderStatus.Ready),
            completed.Count,
            completed.Sum(o => o.Total),
            todays.Count(o => o.Status is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready));
    }

    private async Task<string> NextOrderNumberAsync()
    {
        // derive from the highest existing number — row counts have gaps (cancelled/seeded)
        var prefix = $"ORD-{DateTime.UtcNow:yyyy}-";
        var max = await _db.Orders.IgnoreQueryFilters()
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .OrderByDescending(o => o.OrderNumber) // string ordering is supported by SQLite
            .Select(o => o.OrderNumber)
            .FirstOrDefaultAsync();
        var next = int.TryParse(max?[prefix.Length..], out var n) ? n + 1 : 1;
        return $"{prefix}{next:D5}";
    }

    internal static OrderDto ToDto(Order o) => new(
        o.Id, o.OrderNumber, (int)o.Type, o.Type.ToString(), (int)o.Status, o.Status.ToString(),
        (int)o.PaymentStatus, o.PaymentStatus.ToString(), o.TableId, o.Table?.Number, o.Customer?.FullName ?? o.GuestName,
        o.Phone, o.DeliveryAddress, o.Subtotal, o.DiscountAmount, o.TaxAmount, o.ServiceCharge, o.DeliveryFee,
        o.Tip, o.Total, o.CouponCode, o.Notes, o.EstimatedReadyTime, o.CreatedBy?.FullName, o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(i.Id, i.MenuItemId, i.NameSnapshot, i.UnitPrice, i.Quantity, i.LineTotal,
            i.Notes, i.ModifierText, (int)i.Status, i.Status.ToString())).ToList(),
        o.Payments.Select(p => new PaymentRecordDto(p.Id, (int)p.Method, p.Method.ToString(), p.Amount, p.TransactionRef, p.PaidAt, p.Cashier?.FullName)).ToList());
}

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    public PaymentService(AppDbContext db) => _db = db;

    public async Task<bool> PayAsync(PaymentDto dto, int cashierId)
    {
        var order = await _db.Orders.Include(o => o.Payments).Include(o => o.Customer).FirstAsync(o => o.Id == dto.OrderId);
        if (order.Status == OrderStatus.Cancelled) return false;

        _db.Payments.Add(new Payment
        {
            OrderId = dto.OrderId,
            Method = (PaymentMethod)dto.Method,
            Amount = dto.Amount,
            TransactionRef = dto.TransactionRef ?? $"TXN-{DateTime.UtcNow:yyyyMMddHHmmss}",
            CashierId = cashierId,
            PaidAt = DateTime.UtcNow
        });
        if (dto.Tip.HasValue && dto.Tip > 0) order.Tip = dto.Tip.Value;

        var paid = order.Payments.Sum(p => p.Amount) + dto.Amount;
        order.PaymentStatus = paid >= order.Total ? PaymentStatus.Paid : PaymentStatus.PartiallyPaid;

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            if (order.Status is OrderStatus.Pending or OrderStatus.Confirmed or OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Served)
                order.Status = OrderStatus.Completed;
            if (order.TableId.HasValue)
            {
                var t = await _db.Tables.FindAsync(order.TableId);
                if (t != null) t.Status = TableStatus.Dirty;
            }
            if (order.CustomerId.HasValue)
            {
                var c = await _db.Customers.FindAsync(order.CustomerId);
                if (c != null)
                {
                    var settings = await _db.Settings.AsNoTracking().FirstAsync();
                    c.OrderCount++; c.TotalSpent += order.Total;
                    c.LoyaltyPoints += (int)(order.Total * settings.LoyaltyPointsPerDollar);
                }
            }
        }
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<PaymentRecordDto>> GetOrderPaymentsAsync(int orderId)
    {
        var p = await _db.Payments.Include(x => x.Cashier).Where(x => x.OrderId == orderId).AsNoTracking().ToListAsync();
        return p.Select(x => new PaymentRecordDto(x.Id, (int)x.Method, x.Method.ToString(), x.Amount, x.TransactionRef, x.PaidAt, x.Cashier?.FullName)).ToList();
    }
}
