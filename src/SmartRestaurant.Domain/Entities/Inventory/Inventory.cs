using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Inventory;

public enum StorageType { Dry = 1, Fridge = 2, Freezer = 3 }
public enum MovementType { Purchase = 1, Usage = 2, Waste = 3, Adjustment = 4, Return = 5 }
public enum PurchaseStatus { Draft = 1, Ordered = 2, Received = 3, Cancelled = 4 }

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public double Rating { get; set; } = 4.0;
    public bool IsActive { get; set; } = true;
}

public class Ingredient : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = "kg"; // kg, g, L, pcs
    public decimal StockQty { get; set; }
    public decimal MinStock { get; set; }
    public decimal CostPerUnit { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public StorageType Storage { get; set; } = StorageType.Dry;
    public bool LowStock => StockQty <= MinStock;
}

public class PurchaseOrder : BaseEntity
{
    public string PoNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;
    public DateTime? ExpectedDate { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public int? CreatedById { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal => Quantity * UnitCost;
}

public class StockMovement : BaseEntity
{
    public int IngredientId { get; set; }
    public Ingredient? Ingredient { get; set; }
    public decimal Quantity { get; set; } // signed: + in, - out
    public MovementType Type { get; set; }
    public string? Reason { get; set; }
    public int? OrderId { get; set; }
    public int? CreatedById { get; set; }
    public decimal StockAfter { get; set; }
}
