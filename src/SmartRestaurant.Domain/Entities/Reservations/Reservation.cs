using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Reservations;

public enum ReservationStatus { Pending = 1, Confirmed = 2, Seated = 3, Cancelled = 4, NoShow = 5 }

public class Reservation : BaseEntity
{
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public int PartySize { get; set; } = 2;
    public int? TableId { get; set; }
    public Tables.DiningTable? Table { get; set; }
    public DateTime DateTime { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public string? Occasion { get; set; } // birthday, anniversary...
    public string? Notes { get; set; }
    public string? Source { get; set; } = "Front Desk"; // phone / website / walk-in
}
