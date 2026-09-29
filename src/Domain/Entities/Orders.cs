using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Domain.Entities;

public class DiningTable : BaseEntity, IBranchScoped
{
    public int Number { get; set; }
    public int Seats { get; set; } = 4;
    public TableStatus Status { get; set; }
    public string? QrToken { get; set; }                     // for guest self-order QR
    public Guid? CurrentOrderId { get; set; }
    public Guid? BranchId { get; set; }
}

public class Order : BaseEntity, IBranchScoped
{
    public string OrderNumber { get; set; } = default!;      // e.g. A-1042
    public OrderType Type { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
    public Guid? TableId { get; set; }
    public DiningTable? Table { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? DeliveryAddress { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public string? Note { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? ActiveEventId { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public int ItemsCount => Items?.Sum(i => i.Quantity) ?? 0;
}

public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }
    public string ItemNameSnapshot { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public OrderItemStatus Status { get; set; } = OrderItemStatus.Queued;
    public Station Station { get; set; } = Station.HotKitchen;
    public DateTime? StartedAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public Guid? BranchId { get; set; }
}

public class Reservation : BaseEntity, IBranchScoped
{
    public string CustomerName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public DateTime ReservedFor { get; set; }
    public int PartySize { get; set; }
    public Guid? TableId { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string? Note { get; set; }
    public Guid? BranchId { get; set; }
}
