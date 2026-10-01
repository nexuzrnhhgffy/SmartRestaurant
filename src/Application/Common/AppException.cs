using SmartRestaurant.Application.Common;
using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Application.Common;

/// <summary>Domain error carrying an HTTP status code (messages in Persian — فارسی).</summary>
public class AppException : Exception
{
    public int StatusCode { get; }
    public AppException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;

    /// <summary>Persian names for entities used in NotFound messages.</summary>
    private static readonly Dictionary<string, string> EntityFa = new()
    {
        ["User"] = "کاربر", ["Camera"] = "دوربین", ["Printer"] = "چاپگر",
        ["Notification"] = "اعلان", ["Ingredient"] = "ماده اولیه", ["Branch"] = "شعبه",
        ["Menu item"] = "آیتم منو", ["Event"] = "رویداد", ["Order"] = "سفارش",
        ["Table"] = "میز", ["Order item"] = "آیتم سفارش", ["Reservation"] = "رزرو",
        ["Gateway config"] = "تنظیمات درگاه پرداخت", ["Account"] = "حساب",
        ["Journal"] = "سند حسابداری", ["Expense"] = "هزینه", ["Coupon"] = "کد تخفیف",
        ["Package"] = "پکیج", ["Booking"] = "رزرو رویداد", ["Review"] = "نظر",
        ["Category"] = "دسته‌بندی", ["Shift"] = "شیفت کاری", ["Employee"] = "کارمند"
    };

    public static AppException NotFound(string what) =>
        new($"{EntityFa.GetValueOrDefault(what, what)} یافت نشد.", 404);
    public static AppException BadRequest(string msg) => new(msg, 400);
    public static AppException Forbidden(string msg = "شما مجوز انجام این عملیات را ندارید.") => new(msg, 403);
}

/// <summary>Current authenticated user (populated from JWT claims by the API layer).</summary>
public interface IUserContext
{
    Guid? UserId { get; }
    string? UserName { get; }
    UserRole? Role { get; }
    Guid? BranchId { get; }
    bool IsSuperAdmin { get; }
    bool IsAuthenticated { get; }
    UserRole RoleOr(UserRole fallback) => Role ?? fallback;
}

public class AnonymousUserContext : IUserContext
{
    public Guid? UserId => null;
    public string? UserName => null;
    public UserRole? Role => null;
    public Guid? BranchId => null;
    public bool IsSuperAdmin => false;
    public bool IsAuthenticated => false;
}
