using Microsoft.EntityFrameworkCore;
using SmartRestaurant.Application.Common;
using SmartRestaurant.Application.Contracts;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;
using SmartRestaurant.Infrastructure.Data;

namespace SmartRestaurant.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IHubContext<Hubs.NotificationHub, IAppClient> _notify;
    private readonly ILogger<InventoryService> _log;

    public InventoryService(AppDbContext db, INotificationService notifications, IHubContext<Hubs.NotificationHub, IAppClient> notify, ILogger<InventoryService> log)
        => (_db, _notifications, _notify, _log) = (db, notifications, notify, log);

    public async Task<List<IngredientDto>> GetIngredientsAsync(Guid? branchId)
    {
        var q = _db.Ingredients.AsNoTracking().AsQueryable();
        if (branchId != null) q = q.Where(i => i.BranchId == null || i.BranchId == branchId);
        var list = await q.OrderBy(i => i.Name).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<IngredientDto> UpsertAsync(IngredientUpsertDto dto)
    {
        Ingredient ing;
        if (dto.Id is null)
        {
            ing = new Ingredient { Name = dto.Name, Unit = dto.Unit, MinStock = dto.MinStock, CostPerUnit = dto.CostPerUnit, SupplierName = dto.SupplierName, Stock = 0, BranchId = dto.BranchId };
            _db.Ingredients.Add(ing);
            await _db.SaveChangesAsync();
            if (dto.OpeningStock > 0) await ApplyMovementAsync(ing, dto.OpeningStock, StockMovementType.Purchase, "Opening stock", null);
        }
        else
        {
            ing = await _db.Ingredients.FindAsync(dto.Id) ?? throw AppException.NotFound("Ingredient");
            ing.Name = dto.Name; ing.Unit = dto.Unit; ing.MinStock = dto.MinStock; ing.CostPerUnit = dto.CostPerUnit; ing.SupplierName = dto.SupplierName; ing.BranchId = dto.BranchId;
            await _db.SaveChangesAsync();
        }
        return ToDto(ing);
    }

    public async Task DeleteAsync(Guid id)
    {
        var ing = await _db.Ingredients.FindAsync(id) ?? throw AppException.NotFound("Ingredient");
        ing.IsDeleted = true; ing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public Task ReceiveAsync(StockAdjustDto dto, IUserContext actor)
        => ApplyByIdAsync(dto.IngredientId, dto.Quantity, dto.Type, dto.Note, actor.UserId);

    /*
     * ═══════════ RECIPE → STOCK AUTO-DEDUCTION ═══════════
     * Called when an order reaches Completed. Walks every order line's
     * recipe (BOM), decrements ingredient stock by quantity × ordered qty,
     * records a Usage stock movement referencing the order, and raises a
     * LowStock notification the moment an ingredient crosses its reorder point.
     */
    public async Task DeductForOrderAsync(Guid orderId)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) return;

        var menuItemIds = order.Items.Select(i => i.MenuItemId).ToList();
        var recipes = await _db.RecipeItems.Where(r => menuItemIds.Contains(r.MenuItemId)).ToListAsync();
        if (recipes.Count == 0) { _log.LogInformation("Order {Order} has no recipes — nothing to deduct", order.OrderNumber); return; }

        var ingredientIds = recipes.Select(r => r.IngredientId).Distinct().ToList();
        var ingredients = await _db.Ingredients.Where(i => ingredientIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
        var orderQtyByItem = order.Items.GroupBy(i => i.MenuItemId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        foreach (var group in recipes.GroupBy(r => r.IngredientId))
        {
            var total = group.Sum(r => r.Quantity * orderQtyByItem.GetValueOrDefault(r.MenuItemId));
            if (total <= 0) continue;
            var ing = ingredients[group.Key];
            var wasLow = ing.Stock <= ing.MinStock;
            ing.Stock = Math.Round(ing.Stock - total, 3);
            _db.StockMovements.Add(new StockMovement
            {
                IngredientId = ing.Id, Type = StockMovementType.Usage, Quantity = -total,
                StockAfter = ing.Stock, OrderId = order.Id, Note = $"Auto-deduct for {order.OrderNumber}", BranchId = order.BranchId
            });
            if (!wasLow && ing.Stock <= ing.MinStock)
            {
                await _notifications.CreateAsync(NotificationType.LowStock, $"موجودی کم: {ing.Name}",
                    $"تنها {Fa.DigitsToFa(ing.Stock.ToString("0.0"))} {ing.Unit} باقی مانده (حداقل {Fa.DigitsToFa(ing.MinStock.ToString("0.0"))}). از {ing.SupplierName} سفارش دهید.", order.BranchId, UserRole.Manager);
                await _notifications.CreateAsync(NotificationType.LowStock, $"موجودی کم: {ing.Name}",
                    $"تنها {Fa.DigitsToFa(ing.Stock.ToString("0.0"))} {ing.Unit} باقی مانده.", order.BranchId, UserRole.Inventory);
            }
            _log.LogInformation("Deducted {Qty} {Unit} of {Name} for order {Order} → {Stock}", total, ing.Unit, ing.Name, order.OrderNumber, ing.Stock);
        }
        await _db.SaveChangesAsync();
        await _notify.Clients.All.StatsRefresh("stock-deducted");
    }

    public async Task<List<StockMovementDto>> GetMovementsAsync(Guid? branchId, Guid? ingredientId, int take = 200)
    {
        var q = _db.StockMovements.AsNoTracking().Include(m => m.Ingredient).AsQueryable();
        if (branchId != null) q = q.Where(m => m.BranchId == null || m.BranchId == branchId);
        if (ingredientId != null) q = q.Where(m => m.IngredientId == ingredientId);
        var list = await q.OrderByDescending(m => m.CreatedAt).Take(take).ToListAsync();
        var orderIds = list.Where(m => m.OrderId != null).Select(m => m.OrderId!.Value).ToList();
        var orderNumbers = await _db.Orders.Where(o => orderIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.OrderNumber);
        return list.Select(m => new StockMovementDto
        {
            Id = m.Id, IngredientName = m.Ingredient?.Name ?? "?", Type = m.Type, Quantity = m.Quantity,
            StockAfter = m.StockAfter, Note = m.Note, CreatedAt = m.CreatedAt,
            OrderNumber = m.OrderId != null ? orderNumbers.GetValueOrDefault(m.OrderId.Value) : null
        }).ToList();
    }

    public Task<int> LowStockCountAsync(Guid? branchId) =>
        _db.Ingredients.CountAsync(i => i.Stock <= i.MinStock);

    // ── internals ──
    private Task ApplyByIdAsync(Guid ingredientId, decimal qty, StockMovementType type, string? note, Guid? userId) =>
        ApplyByIdCoreAsync(ingredientId, qty, type, note, userId);

    private async Task ApplyByIdCoreAsync(Guid ingredientId, decimal qty, StockMovementType type, string? note, Guid? userId)
    {
        var ing = await _db.Ingredients.FindAsync(ingredientId) ?? throw AppException.NotFound("Ingredient");
        await ApplyMovementAsync(ing, qty, type, note, userId);
    }

    private async Task ApplyMovementAsync(Ingredient ing, decimal qty, StockMovementType type, string? note, Guid? userId)
    {
        var signed = type switch { StockMovementType.Purchase => qty, StockMovementType.Usage => -Math.Abs(qty), StockMovementType.Waste => -Math.Abs(qty), StockMovementType.ReturnToSupplier => -Math.Abs(qty), _ => qty };
        var wasLow = ing.Stock <= ing.MinStock;
        ing.Stock = Math.Round(ing.Stock + signed, 3);
        ing.UpdatedAt = DateTime.UtcNow;
        _db.StockMovements.Add(new StockMovement { IngredientId = ing.Id, Type = type, Quantity = signed, StockAfter = ing.Stock, Note = note ?? type.ToString(), UserId = userId, BranchId = ing.BranchId });
        await _db.SaveChangesAsync();
        if (!wasLow && ing.Stock <= ing.MinStock)
            await _notifications.CreateAsync(NotificationType.LowStock, $"موجودی کم: {ing.Name}", $"تنها {Fa.DigitsToFa(ing.Stock.ToString("0.0"))} {ing.Unit} باقی مانده (حداقل {Fa.DigitsToFa(ing.MinStock.ToString("0.0"))}).", ing.BranchId, UserRole.Manager);
        await _notify.Clients.All.StatsRefresh("stock-movement");
    }

    internal static IngredientDto ToDto(Ingredient i) => new()
    { Id = i.Id, Name = i.Name, Unit = i.Unit, Stock = i.Stock, MinStock = i.MinStock, CostPerUnit = i.CostPerUnit, SupplierName = i.SupplierName, BranchId = i.BranchId };
}
