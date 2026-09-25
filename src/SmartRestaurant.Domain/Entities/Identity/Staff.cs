using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Identity;

public enum AppRole
{
    SuperAdmin = 1,
    Manager = 2,
    Cashier = 3,
    Waiter = 4,
    Chef = 5,
    Accountant = 6,
    Customer = 7
}

public class AppUser : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public AppRole Role { get; set; } = AppRole.Customer;
    public bool IsActive { get; set; } = true;
    public string? AvatarUrl { get; set; }
    public DateTime? HireDate { get; set; }
    public decimal? BaseSalary { get; set; }
    public string? JobTitle { get; set; }
    public string? PinCode { get; set; } // POS fast login
    public DateTime? LastLoginAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime? RefreshTokenExpiry { get; set; }
}

public enum ShiftStatus { Scheduled = 1, InProgress = 2, Completed = 3, Absent = 4 }

public class Shift : BaseEntity
{
    public int EmployeeId { get; set; }
    public AppUser? Employee { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public ShiftStatus Status { get; set; } = ShiftStatus.Scheduled;
    public string? Notes { get; set; }
}

public class Attendance : BaseEntity
{
    public int EmployeeId { get; set; }
    public AppUser? Employee { get; set; }
    public DateTime? CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public double? HoursWorked => CheckIn.HasValue && CheckOut.HasValue ? (CheckOut - CheckIn).Value.TotalHours : null;
    public string? Notes { get; set; }
}
