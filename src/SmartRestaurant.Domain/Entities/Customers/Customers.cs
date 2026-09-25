using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Customers;

public enum LoyaltyTier { Bronze = 1, Silver = 2, Gold = 3, Platinum = 4 }
public enum CouponType { Percent = 1, Fixed = 2 }
public enum ReviewType { Food = 1, Service = 2, Delivery = 3, Overall = 4 }
public enum ReviewStatus { Pending = 1, Approved = 2, Rejected = 3 }

public class Customer : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateTime? BirthDate { get; set; }
    public int LoyaltyPoints { get; set; }
    public LoyaltyTier Tier => LoyaltyPoints switch
    {
        >= 2000 => LoyaltyTier.Platinum,
        >= 1000 => LoyaltyTier.Gold,
        >= 400 => LoyaltyTier.Silver,
        _ => LoyaltyTier.Bronze
    };
    public decimal TotalSpent { get; set; }
    public int OrderCount { get; set; }
}

public class Review : BaseEntity
{
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Rating { get; set; } = 5; // 1..5
    public string? Comment { get; set; }
    public ReviewType Type { get; set; } = ReviewType.Overall;
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public string? Reply { get; set; } // manager public reply
}

public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CouponType Type { get; set; } = CouponType.Percent;
    public decimal Value { get; set; } // percent (e.g. 15) or fixed amount
    public decimal MinOrderAmount { get; set; }
    public int MaxUses { get; set; } = 100;
    public int UsedCount { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsValid => IsActive
        && UsedCount < MaxUses
        && (ValidFrom == null || ValidFrom <= DateTime.UtcNow)
        && (ValidTo == null || ValidTo >= DateTime.UtcNow);
}
