using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Orders;

public enum OrderType { DineIn = 1, Takeaway = 2, Delivery = 3, Pickup = 4, Event = 5 }
public enum OrderStatus { Pending = 1, Confirmed = 2, Preparing = 3, Ready = 4, Served = 5, Completed = 6, Cancelled = 7 }
public enum PaymentStatus { Unpaid = 1, PartiallyPaid = 2, Paid = 3, Refunded = 4 }
public enum PaymentMethod { Cash = 1, Card = 2, Online = 3, Wallet = 4, QrCode = 5 }
public enum LineItemStatus { Pending = 1, Preparing = 2, Ready = 3, Served = 4, Cancelled = 5 }

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty; // e.g. ORD-2026-00001
    public OrderType Type { get; set; } = OrderType.DineIn;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public int? TableId { get; set; }
    public Tables.DiningTable? Table { get; set; }

    public int? CustomerId { get; set; }
    public Customers.Customer? Customer { get; set; }

    public string? GuestName { get; set; } // walk-in name for online orders
    public string? Phone { get; set; }
    public string? DeliveryAddress { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal Tip { get; set; }
    public decimal Total { get; set; }
    public string? CouponCode { get; set; }
    public string? Notes { get; set; }
    public DateTime? EstimatedReadyTime { get; set; }

    public int? CreatedById { get; set; }
    public Identity.AppUser? CreatedBy { get; set; }

    public int? DriverId { get; set; } // delivery staff assignment
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int MenuItemId { get; set; }
    public Menu.MenuItem? MenuItem { get; set; }
    public string NameSnapshot { get; set; } = string.Empty; // keeps name even if menu changes
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal LineTotal { get; set; }
    public string? ModifierText { get; set; } // "Extra cheese, No onions"
    public string? Notes { get; set; }
    public LineItemStatus Status { get; set; } = LineItemStatus.Pending;
    public decimal CostSnapshot { get; set; } // food cost at sale time
}

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public decimal Amount { get; set; }
    public decimal? ChangeGiven { get; set; }
    public string? TransactionRef { get; set; }
    public int? CashierId { get; set; }
    public Identity.AppUser? Cashier { get; set; }
    public DateTime? PaidAt { get; set; }
}
