using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Domain.Entities;

public class Branch : BaseEntity, IBranchScoped
{
    public string Name { get; set; } = default!;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; } = true;
    public string? TimeZone { get; set; } = "Asia/Tehran";
    public Guid? BranchId { get; set; } // self (null) — allows uniform scoping helpers
}

public class AppUser : BaseEntity, IBranchScoped
{
    public string FullName { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = default!;
    public UserRole Role { get; set; }
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime? RevokedAt { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive => RevokedAt == null && !IsExpired;
}

public class AuditLog : BaseEntity, IBranchScoped
{
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = default!;
    public string? Detail { get; set; }
    public string? Ip { get; set; }
    public Guid? BranchId { get; set; }
}

public class AppSetting : BaseEntity
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
}

public class Notification : BaseEntity, IBranchScoped
{
    public Guid? UserId { get; set; }          // null = role broadcast
    public UserRole? TargetRole { get; set; }  // null = everyone (branch scope)
    public NotificationType Type { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public bool IsRead { get; set; }
    public Guid? BranchId { get; set; }
}

public class RestaurantEvent : BaseEntity, IBranchScoped
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal DiscountPercent { get; set; }
    public string? BannerEmoji { get; set; } = "🎉";
    public bool IsActive { get; set; } = true;
    public Guid? BranchId { get; set; }
}
