using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Application.Common;

// ═══════════════════════════ Auth & Users ═══════════════════════════

public record LoginRequest(string UserName, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc, UserDto User);

public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public string RoleName => Role.ToString();
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class UserUpsertDto
{
    public Guid? Id { get; set; }
    public string FullName { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Password { get; set; }
    public UserRole Role { get; set; }
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BranchDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
}

public class BranchUpsertDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = default!;
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

// ═══════════════════════════ Catalog ═══════════════════════════

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Emoji { get; set; } = default!;
    public int SortOrder { get; set; }
    public Station Station { get; set; }
    public int ItemsCount { get; set; }
}

public class RecipeItemDto
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = default!;
}

public class MenuItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public Guid CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? CategoryEmoji { get; set; }
    public bool IsAvailable { get; set; }
    public int PrepMinutes { get; set; }
    public bool IsSpicy { get; set; }
    public bool IsVegetarian { get; set; }
    public int Calories { get; set; }
    public Guid? BranchId { get; set; }
    public List<RecipeItemDto> Recipe { get; set; } = new();
}

public class MenuItemUpsertDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public Guid CategoryId { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int PrepMinutes { get; set; } = 15;
    public bool IsSpicy { get; set; }
    public bool IsVegetarian { get; set; }
    public int Calories { get; set; }
    public Guid? BranchId { get; set; }
    public List<RecipeLineDto> Recipe { get; set; } = new();
}

public record RecipeLineDto(Guid IngredientId, decimal Quantity);

public class EventDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? BannerEmoji { get; set; }
    public bool IsActive { get; set; }
    public Guid? BranchId { get; set; }
    public bool IsRunning { get; set; }
}

// ═══════════════════════════ Orders ═══════════════════════════

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }
    public string ItemName { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public OrderItemStatus Status { get; set; }
    public Station Station { get; set; }
    public string StationName => Station.ToString();
    public DateTime? StartedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = default!;
    public OrderType Type { get; set; }
    public string TypeName => Type.ToString();
    public OrderStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public PaymentStatus PaymentStatus { get; set; }
    public string PaymentStatusName => PaymentStatus.ToString();
    public Guid? TableId { get; set; }
    public int? TableNumber { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public Guid? BranchId { get; set; }
    public string? CreatedByName { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderCreateDto
{
    public OrderType Type { get; set; } = OrderType.DineIn;
    public Guid? BranchId { get; set; }
    public Guid? TableId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? Note { get; set; }
    public List<OrderLineInput> Items { get; set; } = new();
}

public record OrderLineInput(Guid MenuItemId, int Quantity, string? Notes);

public class OrderStatusUpdateDto
{
    public OrderStatus Status { get; set; }
    public string? Reason { get; set; }
}

public class TableDto
{
    public Guid Id { get; set; }
    public int Number { get; set; }
    public int Seats { get; set; }
    public TableStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? CurrentOrderId { get; set; }
    public string? CurrentOrderNumber { get; set; }
    public decimal? OpenAmount { get; set; }
    public Guid? BranchId { get; set; }
}

public class ReservationDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public DateTime ReservedFor { get; set; }
    public int PartySize { get; set; }
    public Guid? TableId { get; set; }
    public int? TableNumber { get; set; }
    public ReservationStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid? BranchId { get; set; }
}

public class ReservationUpsertDto
{
    public Guid? Id { get; set; }
    public string CustomerName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public DateTime ReservedFor { get; set; }
    public int PartySize { get; set; } = 2;
    public Guid? TableId { get; set; }
    public string? Note { get; set; }
    public Guid? BranchId { get; set; }
}

// ═══════════════════════════ KDS ═══════════════════════════

public class KdsTicketDto
{
    public Guid OrderItemId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = default!;
    public string ItemName { get; set; } = default!;
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public OrderItemStatus Status { get; set; }
    public Station Station { get; set; }
    public string? TableNumber { get; set; }
    public string TypeName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public int ElapsedMinutes { get; set; }
    public bool IsLate => ElapsedMinutes >= 15;
}

public record KdsItemStatusUpdate(OrderItemStatus Status);

// ═══════════════════════════ Inventory ═══════════════════════════

public class IngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public decimal CostPerUnit { get; set; }
    public string? SupplierName { get; set; }
    public bool IsLow => Stock <= MinStock;
    public decimal StockValue => Stock * CostPerUnit;
    public Guid? BranchId { get; set; }
}

public class IngredientUpsertDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = default!;
    public string Unit { get; set; } = "kg";
    public decimal MinStock { get; set; }
    public decimal CostPerUnit { get; set; }
    public string? SupplierName { get; set; }
    public decimal OpeningStock { get; set; }
    public Guid? BranchId { get; set; }
}

public class StockAdjustDto
{
    public Guid IngredientId { get; set; }
    public decimal Quantity { get; set; }                    // +in / -out
    public StockMovementType Type { get; set; } = StockMovementType.Purchase;
    public string? Note { get; set; }
}

public class StockMovementDto
{
    public Guid Id { get; set; }
    public string IngredientName { get; set; } = default!;
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal StockAfter { get; set; }
    public string? OrderNumber { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? UserName { get; set; }
}

// ═══════════════════════════ Payments ═══════════════════════════

public class PaymentInitiateDto
{
    public Guid OrderId { get; set; }
    public PaymentGatewayType Gateway { get; set; } = PaymentGatewayType.Zarinpal;
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = default!;
    public PaymentGatewayType Gateway { get; set; }
    public string GatewayName => Gateway.ToString();
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public string? RefId { get; set; }
    public string? CardPanMasked { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentResultDto
{
    public Guid PaymentId { get; set; }
    public bool IsSandbox { get; set; }
    public string RedirectUrl { get; set; } = default!;
    public string GatewayName { get; set; } = default!;
}

// ═══════════════════════════ Finance & Reports ═══════════════════════════

public class ExpenseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Category { get; set; }
    public decimal Amount { get; set; }
    public DateTime SpentAt { get; set; }
    public string? PaidTo { get; set; }
    public string? Note { get; set; }
    public Guid? BranchId { get; set; }
}

public class ExpenseUpsertDto
{
    public string Title { get; set; } = default!;
    public string? Category { get; set; }
    public decimal Amount { get; set; }
    public DateTime SpentAt { get; set; }
    public string? PaidTo { get; set; }
    public string? Note { get; set; }
    public Guid? BranchId { get; set; }
}

public class JournalEntryDto
{
    public Guid Id { get; set; }
    public string EntryNumber { get; set; } = default!;
    public DateTime PostedAt { get; set; }
    public string Description { get; set; } = default!;
    public string? SourceType { get; set; }
    public List<JournalLineDto> Lines { get; set; } = new();
}

public class JournalLineDto
{
    public string AccountCode { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Memo { get; set; }
}

public class TrialBalanceRow
{
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public int Group { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class ProfitLossDto
{
    public decimal Revenue { get; set; }
    public decimal VatPayable { get; set; }
    public decimal Expenses { get; set; }
    public decimal NetProfit => Revenue - VatPayable - Expenses;
    public int OrdersCount { get; set; }
    public decimal AvgTicket => OrdersCount == 0 ? 0 : Revenue / OrdersCount;
    public List<ExpenseDto> ExpenseBreakdown { get; set; } = new();
}

public class SalesPointDto { public DateTime Date { get; set; } public decimal Revenue { get; set; } public int Orders { get; set; } }

public class TopItemDto { public string Name { get; set; } = default!; public int Qty { get; set; } public decimal Revenue { get; set; } }

public class BranchSalesDto { public Guid BranchId { get; set; } public string Name { get; set; } = default!; public decimal Revenue { get; set; } public int Orders { get; set; } }

public class XReportDto
{
    public string BranchName { get; set; } = default!;
    public DateTime GeneratedAt { get; set; }
    public int OrdersCount { get; set; }
    public int Guests { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal Vat { get; set; }
    public decimal NetSales { get; set; }
    public decimal CashCollected { get; set; }
    public decimal OnlineCollected { get; set; }
    public decimal CardCollected { get; set; }
    public int Voids { get; set; }
}

public class DashboardStatsDto
{
    public decimal TodayRevenue { get; set; }
    public int TodayOrders { get; set; }
    public decimal AvgTicket { get; set; }
    public int OpenOrders { get; set; }
    public int LowStockCount { get; set; }
    public int TablesOccupied { get; set; }
    public int TablesTotal { get; set; }
    public List<SalesPointDto> Last7Days { get; set; } = new();
    public List<TopItemDto> TopItems { get; set; } = new();
    public List<BranchSalesDto> BranchSales { get; set; } = new();
    public EventDto? RunningEvent { get; set; }
}

// ═══════════════════════════ Ops: Cameras / Printers / Settings / Notifications ═══════════════════════════

public class CameraDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public CameraType Type { get; set; }
    public string Url { get; set; } = default!;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public Guid? BranchId { get; set; }
}

public class CameraUpsertDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = default!;
    public CameraType Type { get; set; } = CameraType.Demo;
    public string Url { get; set; } = default!;
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? BranchId { get; set; }
}

public class PrinterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public PrinterType Type { get; set; }
    public string Host { get; set; } = default!;
    public int Port { get; set; }
    public bool IsDefault { get; set; }
    public bool UseRasterMode { get; set; }
    public string Encoding { get; set; } = default!;
    public Guid? BranchId { get; set; }
}

public class SettingDto { public string Key { get; set; } = default!; public string Value { get; set; } = default!; public string? Description { get; set; } }

public class NotificationDto
{
    public Guid Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GatewayConfigDto
{
    public Guid Id { get; set; }
    public PaymentGatewayType Gateway { get; set; }
    public string GatewayName => Gateway.ToString();
    public bool Enabled { get; set; }
    public bool Sandbox { get; set; }
    public string? MerchantKey { get; set; }
}
