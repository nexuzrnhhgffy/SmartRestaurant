namespace SmartRestaurant.Domain.Common;

/// <summary>Base for all persisted entities. Supports soft delete + audit stamps.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}

public interface IBranchScoped
{
    Guid? BranchId { get; set; }
}

// ─────────────────────────────── Enums ───────────────────────────────

public enum UserRole
{
    SuperAdmin = 1,   // full system, all branches, settings & gateway keys
    Manager = 2,      // branch manager: reports, menu, staff, events
    Cashier = 3,      // billing, payments, X/Z report
    Waiter = 4,       // tables, take orders, reservations
    Kitchen = 5,      // KDS operator
    Accountant = 6,   // journals, expenses, P&L
    Customer = 7,     // online ordering account
    Inventory = 8     // stock clerk: ingredients, purchase, movements
}

public enum OrderType { DineIn = 1, Takeaway = 2, Delivery = 3 }

public enum OrderStatus
{
    Pending = 1,      // created (cart submitted / waiter draft)
    Confirmed = 2,    // accepted -> kitchen ticket printed + KDS push
    Preparing = 3,    // kitchen cooking (any item started)
    Ready = 4,        // all items ready
    Served = 5,       // delivered to guest / picked up
    Completed = 6,    // paid & closed -> stock deducted + journal posted
    Cancelled = 7
}

public enum OrderItemStatus { Queued = 1, Preparing = 2, Ready = 3, Delivered = 4, Cancelled = 5 }

public enum PaymentStatus { Unpaid = 1, Pending = 2, Paid = 3, Failed = 4, Refunded = 5 }

public enum PaymentGatewayType
{
    Cash = 1, CardPresent = 2,
    Zarinpal = 10, Zibal = 11, IDPay = 12, PayIr = 13, NextPay = 14,
    Mellat = 20, Saman = 21, Parsian = 22, Pasargad = 23, Novin = 24
}

public enum StockMovementType { Purchase = 1, Usage = 2, Waste = 3, Adjustment = 4, ReturnToSupplier = 5 }

public enum Station { Grill = 1, HotKitchen = 2, Cold = 3, Dessert = 4, Bar = 5 }

public enum TableStatus { Free = 1, Occupied = 2, Reserved = 3,OutOfService = 4 }

public enum ReservationStatus { Pending = 1, Confirmed = 2, Seated = 3, Cancelled = 4, NoShow = 5 }

public enum NotificationType { Info = 1, NewOrder = 2, OrderReady = 3, LowStock = 4, Payment = 5, Reservation = 6, System = 7 }

public enum CameraType { Mjpeg = 1, Hls = 2, Rtsp = 3, Demo = 4 }

public enum PrinterType { Kitchen = 1, Receipt = 2, Bar = 3 }
