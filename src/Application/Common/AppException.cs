using SmartRestaurant.Application.Common;
using SmartRestaurant.Domain.Common;

namespace SmartRestaurant.Application.Common;

/// <summary>Domain error carrying an HTTP status code.</summary>
public class AppException : Exception
{
    public int StatusCode { get; }
    public AppException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
    public static AppException NotFound(string what) => new($"{what} not found.", 404);
    public static AppException BadRequest(string msg) => new(msg, 400);
    public static AppException Forbidden(string msg = "You do not have permission to perform this action.") => new(msg, 403);
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
