using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;
using SmartRestaurant.Infrastructure.Data;
using SmartRestaurant.Infrastructure.Hubs;

namespace SmartRestaurant.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<KdsHub, IKdsClient> _kds;
    private readonly IHubContext<NotificationHub, IAppClient> _notify;
    private readonly INotificationService _notifications;
    private readonly IPrintService _printer;
    private readonly IInventoryService _inventory;
    private readonly IAccountingService _accounting;
    private readonly ISettingsService _settings;
    private readonly ILogger<OrderService> _log;

    public OrderService(AppDbContext db, IHubContext<KdsHub, IKdsClient> kds, IHubContext<NotificationHub, IAppClient> notify,
        INotificationService notifications, IPrintService printer, IInventoryService inventory,
        IAccountingService accounting, ISettingsService settings, ILogger<OrderService> log)
    { _db = db; _kds = kds; _notify = notify; _notifications = notifications; _printer = printer; _inventory = inventory; _accounting = accounting; _settings = settings; _log = log; }

    // ─────────────────────────── CREATE ───────────────────────────
    public async Task<OrderDto> CreateAsync(OrderCreateDto dto, IUserContext actor)
    {
        if (dto.Items.Count == 0) throw AppException.BadRequest("Order must contain at least one item.");
        var branchId = await Scope.ResolveAsync(_db, actor, dto.BranchId);
        var branch = await _db.Branches.FindAsync(branchId);

        var menuIds = dto.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menu = await _db.MenuItems.Include(m => m.Category).Where(m => menuIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id);
        foreach (var line in dto.Items)
            if (!menu.TryGetValue(line.MenuItemId, out var mi) || !mi.IsAvailable)
                throw AppException.BadRequest($"Menu item unavailable: {menu.GetValueOrDefault(line.MenuItemId)?.Name ?? line.MenuItemId.ToString()}");

        var subtotal = dto.Items.Sum(l => menu[l.MenuItemId].Price * l.Quantity);

        // running event discount
        var now = DateTime.UtcNow;
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.IsActive && e.StartAt <= now && e.EndAt >= now && (e.BranchId == null || e.BranchId == branchId));
        var discount = ev != null ? Math.Round(subtotal * ev.DiscountPercent / 100m, 0) : 0m;
        var taxRate = await _settings.GetTaxRateAsync();
        var tax = Math.Round((subtotal - discount) * taxRate / 100m, 0);

        var typePrefix = dto.Type switch { OrderType.DineIn => "A", OrderType.Takeaway => "T", _ => "D" };
        var order = new Order
        {
            OrderNumber = $"{typePrefix}-{Random.Shared.Next(1000, 9999)}",
            Type = dto.Type, BranchId = branchId, CustomerName = dto.CustomerName, CustomerPhone = dto.CustomerPhone,
            DeliveryAddress = dto.DeliveryAddress, Note = dto.Note,
            CreatedByUserId = actor.UserId, CreatedByName = actor.UserName ?? "guest",
            SubTotal = subtotal, Discount = discount, Tax = tax, Total = subtotal - discount + tax,
            ActiveEventId = ev?.Id,
        };
        foreach (var line in dto.Items)
        {
            var mi = menu[line.MenuItemId];
            order.Items.Add(new OrderItem
            {
                MenuItemId = mi.Id, ItemNameSnapshot = mi.Name, UnitPrice = mi.Price, Quantity = line.Quantity,
                Notes = line.Notes, Station = mi.Category?.Station ?? Station.HotKitchen, BranchId = branchId
            });
        }
        _db.Orders.Add(order);

        if (dto.Type == OrderType.DineIn && dto.TableId != null)
        {
            var table = await _db.Tables.FindAsync(dto.TableId) ?? throw AppException.NotFound("Table");
            table.Status = TableStatus.Occupied; table.CurrentOrderId = order.Id;
            order.TableId = table.Id;
        }
        await _db.SaveChangesAsync();

        _log.LogInformation("Order {OrderNumber} created by {Actor} at branch {BranchId}", order.OrderNumber, actor.UserName, branchId);
        await PushOrderAsync(order);
        await _notifications.CreateAsync(NotificationType.NewOrder, $"New order {order.OrderNumber}",
            $"{dto.Type} • {order.ItemsCount} items • {order.Total:N0} {(await _settings.GetAsync("Restaurant:Currency", "Toman"))}", branchId, UserRole.Cashier);
        await _notify.Clients.Groups($"branch:{branchId}").StatsRefresh("order-created");
        return await GetAsync(order.Id);
    }

    // ─────────────────────────── QUERIES ───────────────────────────
    public async Task<OrderDto> GetAsync(Guid id)
    {
        var o = await _db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Table).Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Order");
        return ToDto(o);
    }

    public async Task<List<OrderDto>> ListAsync(Guid? branchId, OrderStatus? status, OrderType? type, DateTime? from, int take = 100)
    {
        var q = _db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.Table).AsQueryable();
        if (branchId != null) q = q.Where(o => o.BranchId == branchId);
        if (status != null) q = q.Where(o => o.Status == status);
        if (type != null) q = q.Where(o => o.Type == type);
        if (from != null) q = q.Where(o => o.CreatedAt >= from);
        var list = await q.OrderByDescending(o => o.CreatedAt).Take(take).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public Task<List<OrderDto>> ListActiveAsync(Guid? branchId)
    {
        var active = new[] { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served };
        return ListAsync(branchId, null, null, DateTime.UtcNow.AddDays(-2), 300)
            .ContinueWith(t => t.Result.Where(o => active.Contains(o.Status)).ToList());
    }

    // ─────────────────────────── STATE MACHINE ───────────────────────────
    public async Task<OrderDto> UpdateStatusAsync(Guid id, OrderStatusUpdateDto dto, IUserContext actor)
    {
        var o = await _db.Orders.Include(x => x.Items).Include(x => x.Table).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw AppException.NotFound("Order");

        switch (dto.Status)
        {
            case OrderStatus.Confirmed:
                if (o.Status != OrderStatus.Pending) throw AppException.BadRequest($"Only pending orders can be confirmed (current: {o.Status}).");
                o.Status = OrderStatus.Confirmed; o.ConfirmedAt = DateTime.UtcNow;
                await _printer.PrintKitchenTicketAsync(o.Id);       // ESC/POS → kitchen printer
                await PushKdsTicketsAsync(o);                       // SignalR → KDS screens
                break;

            case OrderStatus.Preparing:
                o.Status = OrderStatus.Preparing;
                break;

            case OrderStatus.Ready:
                o.Status = OrderStatus.Ready; o.ReadyAt = DateTime.UtcNow;
                await _notifications.CreateAsync(NotificationType.OrderReady, $"Order {o.OrderNumber} ready",
                    $"All items are ready for {o.Type}.", o.BranchId, o.Type == OrderType.DineIn ? UserRole.Waiter : UserRole.Cashier);
                break;

            case OrderStatus.Served:
                o.Status = OrderStatus.Served;
                break;

            case OrderStatus.Completed:
                if (o.PaymentStatus != PaymentStatus.Paid)
                    throw AppException.BadRequest("Order must be paid before completion. Take payment first.");
                o.Status = OrderStatus.Completed; o.CompletedAt = DateTime.UtcNow;
                if (o.Table != null) { o.Table.Status = TableStatus.Free; o.Table.CurrentOrderId = null; }
                await _inventory.DeductForOrderAsync(o.Id);          // recipe → stock auto-deduction
                await _accounting.PostSaleAsync(o.Id);               // double-entry journal
                await _printer.PrintReceiptAsync(o.Id, null);        // guest receipt
                break;

            case OrderStatus.Cancelled:
                if (o.Status == OrderStatus.Completed) throw AppException.BadRequest("Completed orders cannot be cancelled — issue a refund instead.");
                o.Status = OrderStatus.Cancelled;
                if (o.Table != null) { o.Table.Status = TableStatus.Free; o.Table.CurrentOrderId = null; }
                foreach (var item in o.Items.Where(i => i.Status != OrderItemStatus.Delivered)) item.Status = OrderItemStatus.Cancelled;
                await _kds.Clients.Groups($"branch:{o.BranchId}").OrderStatusChanged(o.Id, o.OrderNumber, "Cancelled");
                break;
        }
        o.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await PushOrderAsync(o);
        await _notify.Clients.Groups($"branch:{o.BranchId}").StatsRefresh($"order-{dto.Status}");
        return await GetAsync(id);
    }

    // KDS per-item bump bar
    public async Task<OrderDto> UpdateItemStatusAsync(Guid orderItemId, OrderItemStatus status, IUserContext actor)
    {
        var item = await _db.OrderItems.Include(i => i.Order).ThenInclude(o => o!.Items)
            .FirstOrDefaultAsync(i => i.Id == orderItemId) ?? throw AppException.NotFound("Order item");

        item.Status = status;
        if (status == OrderItemStatus.Preparing) item.StartedAt ??= DateTime.UtcNow;
        if (status == OrderItemStatus.Ready) item.ReadyAt = DateTime.UtcNow;

        var o = item.Order!;
        if (status == OrderItemStatus.Ready && o.Items.All(i => i.Status is OrderItemStatus.Ready or OrderItemStatus.Delivered or OrderItemStatus.Cancelled) && o.Status is OrderStatus.Confirmed or OrderStatus.Preparing)
        {
            o.Status = OrderStatus.Ready; o.ReadyAt = DateTime.UtcNow;
            await _notifications.CreateAsync(NotificationType.OrderReady, $"Order {o.OrderNumber} ready", "All items bumped ready.", o.BranchId, o.Type == OrderType.DineIn ? UserRole.Waiter : UserRole.Cashier);
        }
        else if (status == OrderItemStatus.Preparing && o.Status == OrderStatus.Confirmed)
            o.Status = OrderStatus.Preparing;

        o.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var ticket = KdsTicket(item, o);
        await _kds.Clients.Groups($"branch:{o.BranchId}").TicketUpdated(ticket);
        await _kds.Clients.Groups($"branch:{o.BranchId}").OrderStatusChanged(o.Id, o.OrderNumber, o.Status.ToString());
        await PushOrderAsync(o);
        return await GetAsync(o.Id);
    }

    public async Task<List<KdsTicketDto>> GetKdsTicketsAsync(Guid? branchId, Station? station)
    {
        var cutoff = DateTime.UtcNow.AddHours(-6);
        var q = _db.OrderItems.AsNoTracking().Include(i => i.Order).ThenInclude(o => o!.Table)
            .Where(i => i.CreatedAt >= cutoff && i.Status != OrderItemStatus.Cancelled && i.Order!.Status != OrderStatus.Cancelled && i.Order.Status != OrderStatus.Completed);
        if (branchId != null) q = q.Where(i => i.BranchId == branchId);
        if (station != null) q = q.Where(i => i.Station == station);
        var items = await q.OrderBy(i => i.Order!.CreatedAt).ToListAsync();
        return items.Select(i => KdsTicket(i, i.Order!)).ToList();
    }

    // ─────────────────────────── internals ───────────────────────────
    private async Task PushKdsTicketsAsync(Order o)
    {
        foreach (var item in o.Items.Where(i => i.Status == OrderItemStatus.Queued))
            await _kds.Clients.Groups($"branch:{o.BranchId}").TicketCreated(KdsTicket(item, o));
    }

    private async Task PushOrderAsync(Order o)
    {
        await _notify.Clients.Groups($"branch:{o.BranchId}").OrderUpdated(ToDto(o));
    }

    internal static KdsTicketDto KdsTicket(OrderItem i, Order o) => new()
    {
        OrderItemId = i.Id, OrderId = o.Id, OrderNumber = o.OrderNumber, ItemName = i.ItemNameSnapshot,
        Quantity = i.Quantity, Notes = i.Notes, Status = i.Status, Station = i.Station,
        TableNumber = o.Table?.Number.ToString() ?? o.Type.ToString(),
        TypeName = o.Type.ToString(), CreatedAt = i.Order?.CreatedAt ?? DateTime.UtcNow,
        ElapsedMinutes = (int)(DateTime.UtcNow - (i.Order?.CreatedAt ?? DateTime.UtcNow)).TotalMinutes
    };

    internal static OrderDto ToDto(Order o) => new()
    {
        Id = o.Id, OrderNumber = o.OrderNumber, Type = o.Type, Status = o.Status, PaymentStatus = o.PaymentStatus,
        TableId = o.TableId, TableNumber = o.Table?.Number, CustomerName = o.CustomerName, CustomerPhone = o.CustomerPhone,
        DeliveryAddress = o.DeliveryAddress, BranchId = o.BranchId, CreatedByName = o.CreatedByName,
        SubTotal = o.SubTotal, Discount = o.Discount, Tax = o.Tax, Total = o.Total, Note = o.Note, CreatedAt = o.CreatedAt,
        CompletedAt = o.CompletedAt,
        Items = o.Items.Select(i => new OrderItemDto
        {
            Id = i.Id, MenuItemId = i.MenuItemId, ItemName = i.ItemNameSnapshot, UnitPrice = i.UnitPrice,
            Quantity = i.Quantity, Notes = i.Notes, Status = i.Status, Station = i.Station, StartedAt = i.StartedAt, ReadyAt = i.ReadyAt
        }).ToList()
    };
}

// ═══════════════════════════ Tables ═══════════════════════════
public class TableService : ITableService
{
    private readonly AppDbContext _db;
    public TableService(AppDbContext db) => _db = db;

    public async Task<List<TableDto>> GetTablesAsync(Guid? branchId)
    {
        var q = _db.Tables.AsNoTracking().AsQueryable();
        if (branchId != null) q = q.Where(t => t.BranchId == branchId);
        var tables = await q.OrderBy(t => t.Number).ToListAsync();
        var orderIds = tables.Where(t => t.CurrentOrderId != null).Select(t => t.CurrentOrderId!.Value).ToList();
        var orders = await _db.Orders.Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o);
        return tables.Select(t =>
        {
            orders.TryGetValue(t.CurrentOrderId ?? Guid.Empty, out var o);
            return new TableDto { Id = t.Id, Number = t.Number, Seats = t.Seats, Status = t.Status, CurrentOrderId = t.CurrentOrderId, CurrentOrderNumber = o?.OrderNumber, OpenAmount = o?.Total, BranchId = t.BranchId };
        }).ToList();
    }

    public async Task<TableDto> UpsertAsync(TableDto dto)
    {
        DiningTable t;
        if (dto.Id == Guid.Empty) { t = new DiningTable(); _db.Tables.Add(t); }
        else t = await _db.Tables.FindAsync(dto.Id) ?? throw AppException.NotFound("Table");
        t.Number = dto.Number; t.Seats = dto.Seats; t.Status = dto.Status; t.BranchId = dto.BranchId ?? t.BranchId;
        await _db.SaveChangesAsync();
        return new TableDto { Id = t.Id, Number = t.Number, Seats = t.Seats, Status = t.Status, CurrentOrderId = t.CurrentOrderId, BranchId = t.BranchId };
    }

    public async Task FreeAsync(Guid tableId)
    {
        var t = await _db.Tables.FindAsync(tableId) ?? throw AppException.NotFound("Table");
        t.Status = TableStatus.Free; t.CurrentOrderId = null;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var t = await _db.Tables.FindAsync(id) ?? throw AppException.NotFound("Table");
        t.IsDeleted = true;
        await _db.SaveChangesAsync();
    }
}

// ═══════════════════════════ Reservations ═══════════════════════════
public class ReservationService : IReservationService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IHubContext<NotificationHub, IAppClient> _notify;
    public ReservationService(AppDbContext db, INotificationService notifications, IHubContext<NotificationHub, IAppClient> notify)
        => (_db, _notifications, _notify) = (db, notifications, notify);

    public async Task<List<ReservationDto>> GetAllAsync(Guid? branchId, DateTime? day = null)
    {
        var q = _db.Reservations.AsNoTracking().AsQueryable();
        if (branchId != null) q = q.Where(r => r.BranchId == branchId);
        if (day != null)
        {
            var d = day.Value.Date;
            q = q.Where(r => r.ReservedFor >= d && r.ReservedFor < d.AddDays(1));
        }
        var list = await q.OrderByDescending(r => r.ReservedFor).Take(200).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<ReservationDto> UpsertAsync(ReservationUpsertDto dto)
    {
        Reservation r;
        if (dto.Id is null) { r = new Reservation(); _db.Reservations.Add(r); }
        else r = await _db.Reservations.FindAsync(dto.Id) ?? throw AppException.NotFound("Reservation");
        r.CustomerName = dto.CustomerName; r.Phone = dto.Phone; r.ReservedFor = dto.ReservedFor;
        r.PartySize = dto.PartySize; r.TableId = dto.TableId; r.Note = dto.Note; r.BranchId = dto.BranchId;
        await _db.SaveChangesAsync();
        await _notifications.CreateAsync(NotificationType.Reservation, "New reservation",
            $"{r.CustomerName} • {r.PartySize} guests • {r.ReservedFor:MMM dd HH:mm}", r.BranchId, UserRole.Manager);
        await _notify.Clients.Groups($"branch:{r.BranchId}").StatsRefresh("reservation");
        return ToDto(r);
    }

    public async Task<ReservationDto> SetStatusAsync(Guid id, ReservationStatus status)
    {
        var r = await _db.Reservations.FindAsync(id) ?? throw AppException.NotFound("Reservation");
        r.Status = status;
        if (status == ReservationStatus.Seated && r.TableId != null)
        {
            var t = await _db.Tables.FindAsync(r.TableId);
            if (t != null) { t.Status = TableStatus.Occupied; }
        }
        await _db.SaveChangesAsync();
        return ToDto(r);
    }

    internal static ReservationDto ToDto(Reservation r) => new()
    { Id = r.Id, CustomerName = r.CustomerName, Phone = r.Phone, ReservedFor = r.ReservedFor, PartySize = r.PartySize, TableId = r.TableId, Status = r.Status, Note = r.Note, BranchId = r.BranchId };
}
