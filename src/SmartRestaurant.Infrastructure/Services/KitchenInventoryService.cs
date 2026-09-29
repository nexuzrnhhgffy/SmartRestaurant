using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Dtos;
using SmartRestaurant.Application.Interfaces;
using SmartRestaurant.Domain.Entities.Inventory;
using SmartRestaurant.Domain.Entities.Menu;
using SmartRestaurant.Domain.Entities.Orders;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class KitchenService : IKitchenService
{
    private readonly AppDbContext _db;
    public KitchenService(AppDbContext db) => _db = db;

    public async Task<KitchenQueueDto> GetQueueAsync()
    {
        var activeStatuses = new[] { OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Pending };
        var orders = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items.Where(i => i.Status != LineItemStatus.Served && i.Status != LineItemStatus.Cancelled))
            .Where(o => activeStatuses.Contains(o.Status))
            .OrderBy(o => o.CreatedAt)
            .AsNoTracking().ToListAsync();

        // tickets already fired and waiting on the pass
        var readyOrders = await _db.Orders
            .Include(o => o.Table)
            .Include(o => o.Items.Where(i => i.Status == LineItemStatus.Ready))
            .Where(o => o.Status == OrderStatus.Ready)
            .OrderBy(o => o.CreatedAt).AsNoTracking().ToListAsync();

        var active = orders.Select(o => new KitchenTicketDto(o.Id, o.OrderNumber, (int)o.Type, o.Type.ToString(),
                o.Table?.Number.ToString(), o.CreatedAt, (int)(DateTime.UtcNow - o.CreatedAt).TotalMinutes,
                o.Items.Select(i => new KitchenLineDto(i.Id, i.NameSnapshot, i.Quantity, i.ModifierText, i.Notes,
                    (int)i.Status, i.Status.ToString(), (int)(i.MenuItem != null ? i.MenuItem.Station : PrepStation.HotLine))).ToList()))
            .Where(t => t.Lines.Any()).ToList();

        var ready = readyOrders.Select(o => new KitchenTicketDto(o.Id, o.OrderNumber, (int)o.Type, o.Type.ToString(),
                o.Table?.Number.ToString(), o.CreatedAt, (int)(DateTime.UtcNow - o.CreatedAt).TotalMinutes,
                o.Items.Where(i => i.Status == LineItemStatus.Ready).Select(i => new KitchenLineDto(i.Id, i.NameSnapshot, i.Quantity, i.ModifierText, i.Notes,
                    (int)i.Status, i.Status.ToString(), (int)PrepStation.HotLine)).ToList())).ToList();

        return new KitchenQueueDto(active, ready);
    }
}

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notify;
    public InventoryService(AppDbContext db, INotificationService notify) { _db = db; _notify = notify; }

    public async Task<List<IngredientDto>> GetIngredientsAsync(bool lowOnly = false)
    {
        var q = _db.Ingredients.Include(i => i.Supplier).AsNoTracking();
        var list = await q.OrderBy(i => i.Name).ToListAsync();
        if (lowOnly) list = list.Where(i => i.LowStock).ToList();
        return list.Select(ToDto).ToList();
    }

    public async Task<IngredientDto> SaveIngredientAsync(SaveIngredientDto dto)
    {
        Ingredient ing;
        if (dto.Id is null or 0)
        {
            ing = new Ingredient();
            _db.Ingredients.Add(ing);
        }
        else ing = await _db.Ingredients.FirstAsync(i => i.Id == dto.Id);
        ing.Name = dto.Name; ing.Unit = dto.Unit; ing.StockQty = dto.StockQty; ing.MinStock = dto.MinStock;
        ing.CostPerUnit = dto.CostPerUnit; ing.SupplierId = dto.SupplierId; ing.Storage = (StorageType)dto.Storage;
        ing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        ing = await _db.Ingredients.Include(i => i.Supplier).FirstAsync(i => i.Id == ing.Id);
        return ToDto(ing);
    }

    public async Task<bool> DeleteIngredientAsync(int id)
    {
        var i = await _db.Ingredients.FindAsync(id);
        if (i == null) return false;
        _db.Ingredients.Remove(i);
        await _db.SaveChangesAsync();
        return true;
    }

    public Task<List<SupplierDto>> GetSuppliersAsync() =>
        _db.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync().ContinueWith(t =>
            t.Result.Select(s => new SupplierDto(s.Id, s.Name, s.ContactName, s.Phone, s.Email, s.Address, s.Rating, s.IsActive)).ToList());

    public async Task<SupplierDto> SaveSupplierAsync(SaveSupplierDto dto)
    {
        Supplier s;
        if (dto.Id is null or 0) { s = new Supplier(); _db.Suppliers.Add(s); }
        else s = await _db.Suppliers.FirstAsync(x => x.Id == dto.Id);
        s.Name = dto.Name; s.ContactName = dto.ContactName; s.Phone = dto.Phone; s.Email = dto.Email;
        s.Address = dto.Address; s.Rating = dto.Rating; s.IsActive = dto.IsActive;
        await _db.SaveChangesAsync();
        return new SupplierDto(s.Id, s.Name, s.ContactName, s.Phone, s.Email, s.Address, s.Rating, s.IsActive);
    }

    public async Task<List<PurchaseOrderDto>> GetPurchaseOrdersAsync(int? status = null)
    {
        var q = _db.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Items).ThenInclude(i => i.Ingredient).AsNoTracking();
        if (status.HasValue) q = q.Where(p => (int)p.Status == status);
        var list = await q.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<PurchaseOrderDto> SavePurchaseOrderAsync(SavePurchaseOrderDto dto, int userId)
    {
        var po = new PurchaseOrder
        {
            PoNumber = $"PO-{DateTime.UtcNow:yyyy}-{await _db.PurchaseOrders.CountAsync() + 1:D4}",
            SupplierId = dto.SupplierId,
            ExpectedDate = dto.ExpectedDate,
            Notes = dto.Notes,
            CreatedById = userId,
            Status = PurchaseStatus.Ordered,
            Items = dto.Items.Select(i => new PurchaseOrderItem { IngredientId = i.IngredientId, Quantity = i.Quantity, UnitCost = i.UnitCost }).ToList()
        };
        po.Total = po.Items.Sum(i => i.Quantity * i.UnitCost);
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();
        return ToDto(await _db.PurchaseOrders.Include(p => p.Supplier).Include(p => p.Items).ThenInclude(i => i.Ingredient).FirstAsync(p => p.Id == po.Id));
    }

    public async Task<bool> ReceivePurchaseOrderAsync(int id)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id);
        if (po == null || po.Status == PurchaseStatus.Received) return false;
        foreach (var item in po.Items)
        {
            var ing = await _db.Ingredients.FindAsync(item.IngredientId);
            if (ing == null) continue;
            ing.StockQty += item.Quantity;
            _db.StockMovements.Add(new StockMovement
            {
                IngredientId = ing.Id, Quantity = item.Quantity, Type = MovementType.Purchase,
                Reason = $"Received {po.PoNumber}", StockAfter = ing.StockQty, CreatedById = po.CreatedById
            });
        }
        po.Status = PurchaseStatus.Received;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<StockMovementDto>> GetMovementsAsync(int? ingredientId = null)
    {
        var q = _db.StockMovements.Include(m => m.Ingredient).AsNoTracking();
        if (ingredientId.HasValue) q = q.Where(m => m.IngredientId == ingredientId);
        var list = await q.OrderByDescending(m => m.CreatedAt).Take(200).ToListAsync();
        return list.Select(m => new StockMovementDto(m.Id, m.IngredientId, m.Ingredient?.Name ?? "-", m.Quantity,
            (int)m.Type, m.Type.ToString(), m.Reason, m.StockAfter, m.CreatedAt)).ToList();
    }

    public async Task<bool> AdjustStockAsync(StockAdjustDto dto, int userId)
    {
        var ing = await _db.Ingredients.FindAsync(dto.IngredientId);
        if (ing == null) return false;
        ing.StockQty += dto.Quantity;
        _db.StockMovements.Add(new StockMovement
        {
            IngredientId = ing.Id, Quantity = dto.Quantity, Type = (MovementType)dto.Type,
            Reason = dto.Reason, StockAfter = ing.StockQty, CreatedById = userId
        });
        await _db.SaveChangesAsync();

        if (ing.LowStock)
            await _notify.PushAsync("Low stock alert", $"{ing.Name} ({ing.StockQty:F1} {ing.Unit}) is at/below minimum ({ing.MinStock:F1} {ing.Unit}).", 2, Domain.Entities.Identity.AppRole.Manager, "/admin#inventory");
        return true;
    }

    internal static IngredientDto ToDto(Ingredient i) => new(i.Id, i.Name, i.Unit, i.StockQty, i.MinStock, i.CostPerUnit,
        i.SupplierId, i.Supplier?.Name, (int)i.Storage, i.Storage.ToString(), i.LowStock, i.StockQty * i.CostPerUnit);

    internal static PurchaseOrderDto ToDto(PurchaseOrder p) => new(p.Id, p.PoNumber, p.SupplierId, p.Supplier?.Name ?? "-",
        (int)p.Status, p.Status.ToString(), p.ExpectedDate, p.Total, p.Notes, p.CreatedAt,
        p.Items.Select(i => new PurchaseItemDto(i.Id, i.IngredientId, i.Ingredient?.Name ?? "-", i.Quantity, i.UnitCost, i.Quantity * i.UnitCost)).ToList());
}
