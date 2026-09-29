using SmartRestaurant.Domain.Entities.Menu;

namespace SmartRestaurant.Application.Dtos;

// ---------- Menu ----------
public record MenuItemDto(int Id, int CategoryId, string CategoryName, string Name, string? Description,
    decimal Price, string? ImageUrl, bool IsAvailable, bool IsFeatured, bool IsVegetarian, bool IsSpicy,
    bool IsGlutenFree, int PrepTimeMinutes, int? Calories, string? Allergens, int Station, int TimesOrdered,
    List<ModifierGroupDto> ModifierGroups);

public record ModifierGroupDto(int Id, string Name, int MinSelection, int MaxSelection, List<ModifierOptionDto> Options);
public record ModifierOptionDto(int Id, string Name, decimal ExtraPrice);
public record SaveMenuItemDto(int? Id, int CategoryId, string Name, string? Description, decimal Price, decimal Cost,
    string? ImageUrl, bool IsAvailable, bool IsFeatured, bool IsVegetarian, bool IsSpicy, bool IsGlutenFree,
    int PrepTimeMinutes, int? Calories, string? Allergens, int Station);
public record SaveCategoryDto(int? Id, string Name, string? Description, int SortOrder, bool IsActive, string? ImageUrl);
public record MenuItemAvailabilityDto(int Id, bool IsAvailable);

// ---------- Tables ----------
public record TableDto(int Id, int Number, int Section, string SectionName, int Capacity, int Status, string StatusName, string QrToken, string? Notes);
public record SaveTableDto(int? Id, int Number, int Section, int Capacity, int Status, string? Notes);
public record UpdateTableStatusDto(int Status);

// ---------- Reservations ----------
public record ReservationDto(int Id, string CustomerName, string Phone, string? Email, int PartySize, int? TableId,
    DateTime DateTime, int Status, string StatusName, string? Occasion, string? Notes, string? Source);
public record SaveReservationDto(int? Id, string CustomerName, string Phone, string? Email, int PartySize,
    int? TableId, DateTime DateTime, string? Occasion, string? Notes, string? Source);

// ---------- Coupons ----------
public record CouponDto(int Id, string Code, string? Description, int Type, decimal Value, decimal MinOrderAmount,
    int MaxUses, int UsedCount, DateTime? ValidFrom, DateTime? ValidTo, bool IsActive, bool IsValid);
public record SaveCouponDto(int? Id, string Code, string? Description, int Type, decimal Value, decimal MinOrderAmount,
    int MaxUses, DateTime? ValidFrom, DateTime? ValidTo, bool IsActive);
public record ValidateCouponDto(string Code, decimal OrderAmount);
public record CouponValidationResult(bool Valid, string Message, decimal Discount);
