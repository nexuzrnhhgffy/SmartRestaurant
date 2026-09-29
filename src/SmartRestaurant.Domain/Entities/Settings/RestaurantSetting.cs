using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Settings;

public class RestaurantSetting : BaseEntity
{
    public string Name { get; set; } = "Aurora Smart Restaurant";
    public string? Tagline { get; set; } = "Fine dining, intelligently served";
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal TaxRate { get; set; } = 0.09m;
    public decimal ServiceChargeRate { get; set; } = 0.05m; // dine-in only
    public decimal DeliveryFee { get; set; } = 3.50m;
    public string OpeningHours { get; set; } = "10:00 - 23:30";
    public int LoyaltyPointsPerDollar { get; set; } = 10;
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public bool OnlineOrderingEnabled { get; set; } = true;
    public bool ReservationsEnabled { get; set; } = true;
    public int MaxGuestsPerReservation { get; set; } = 20;
    public int EstimatedDeliveryMinutes { get; set; } = 45;
}
