using SmartRestaurant.Domain.Entities.Identity;

namespace SmartRestaurant.Application.Dtos;

// ---------- Auth ----------
public record LoginRequest(string Email, string Password);
public record RegisterRequest(string FullName, string Email, string Phone, string Password);
public record AuthResponse(bool Success, string Message, string? Token, string? RefreshToken, UserDto? User);
public record UserDto(int Id, string FullName, string Email, string Phone, AppRole Role, bool IsActive,
    string? JobTitle, string? AvatarUrl, DateTime? HireDate, decimal? BaseSalary, DateTime? LastLoginAt);
public record PinLoginRequest(int UserId, string Pin);

// ---------- Common ----------
public record ApiResponse(bool Success, string Message, object? Data = null);
public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
public record CreateOrderItemDto(int MenuItemId, int Quantity, string? Notes, string? ModifierText);
public record CreateOrderDto(int Type, int? TableId, int? CustomerId, string? GuestName, string? Phone,
    string? DeliveryAddress, string? CouponCode, string? Notes, List<CreateOrderItemDto> Items);
public record UpdateOrderStatusDto(int Status);
public record UpdateOrderItemStatusDto(int Status);
public record PaymentDto(int OrderId, int Method, decimal Amount, decimal? Tip, string? TransactionRef, decimal? ChangeGiven);
