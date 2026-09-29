using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Domain.Entities;

public class Ingredient : BaseEntity, IBranchScoped
{
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = "kg";                 // kg / g / l / pcs
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }                    // reorder point
    public decimal CostPerUnit { get; set; }                 // Toman
    public string? SupplierName { get; set; }
    public Guid? BranchId { get; set; }
    public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
}

public class StockMovement : BaseEntity, IBranchScoped
{
    public Guid IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }                    // signed (+in / -out)
    public decimal StockAfter { get; set; }
    public Guid? OrderId { get; set; }
    public string? Note { get; set; }
    public Guid? UserId { get; set; }
    public Guid? BranchId { get; set; }
}
