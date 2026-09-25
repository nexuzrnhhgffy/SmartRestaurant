using SmartRestaurant.Domain.Entities.Orders;

namespace SmartRestaurant.Application.Dtos;

// ---------- Orders ----------
public record OrderItemDto(int Id, int MenuItemId, string Name, decimal UnitPrice, int Quantity, decimal LineTotal,
    string? Notes, string? ModifierText, int Status, string StatusName);
public record OrderDto(int Id, string OrderNumber, int Type, string TypeName, int Status, string StatusName,
    int PaymentStatus, string PaymentStatusName, int? TableId, int? TableNumber, string? CustomerName, string? Phone,
    string? DeliveryAddress, decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal ServiceCharge,
    decimal DeliveryFee, decimal Tip, decimal Total, string? CouponCode, string? Notes, DateTime? EstimatedReadyTime,
    string? CreatedByName, DateTime CreatedAt, List<OrderItemDto> Items, List<PaymentRecordDto> Payments);
public record PaymentRecordDto(int Id, int Method, string MethodName, decimal Amount, string? TransactionRef, DateTime? PaidAt, string? CashierName);
public record OrderStatsDto(int Pending, int Preparing, int ReadyToday, int CompletedToday, decimal RevenueToday, int ActiveOrders);

// ---------- Kitchen (KDS) ----------
public record KitchenTicketDto(int OrderId, string OrderNumber, int Type, string TypeName, string? TableNumber,
    DateTime CreatedAt, int MinutesElapsed, List<KitchenLineDto> Lines);
public record KitchenLineDto(int OrderItemId, string ItemName, int Quantity, string? ModifierText, string? Notes,
    int Status, string StatusName, int Station);
public record KitchenQueueDto(List<KitchenTicketDto> Active, List<KitchenTicketDto> Ready);

// ---------- Inventory ----------
public record IngredientDto(int Id, string Name, string Unit, decimal StockQty, decimal MinStock, decimal CostPerUnit,
    int? SupplierId, string? SupplierName, int Storage, string StorageName, bool LowStock, decimal StockValue);
public record SaveIngredientDto(int? Id, string Name, string Unit, decimal StockQty, decimal MinStock, decimal CostPerUnit,
    int? SupplierId, int Storage);
public record SupplierDto(int Id, string Name, string? ContactName, string Phone, string? Email, string? Address, double Rating, bool IsActive);
public record SaveSupplierDto(int? Id, string Name, string? ContactName, string Phone, string? Email, string? Address, double Rating, bool IsActive);
public record PurchaseOrderDto(int Id, string PoNumber, int SupplierId, string SupplierName, int Status, string StatusName,
    DateTime? ExpectedDate, decimal Total, string? Notes, DateTime CreatedAt, List<PurchaseItemDto> Items);
public record PurchaseItemDto(int Id, int IngredientId, string IngredientName, decimal Quantity, decimal UnitCost, decimal LineTotal);
public record SavePurchaseOrderDto(int? Id, int SupplierId, DateTime? ExpectedDate, string? Notes,
    List<SavePurchaseItemDto> Items);
public record SavePurchaseItemDto(int IngredientId, decimal Quantity, decimal UnitCost);
public record StockMovementDto(int Id, int IngredientId, string IngredientName, decimal Quantity, int Type,
    string TypeName, string? Reason, decimal StockAfter, DateTime CreatedAt);
public record StockAdjustDto(int IngredientId, decimal Quantity, int Type, string? Reason);
