using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Events;

public enum EventType { Wedding = 1, Corporate = 2, Birthday = 3, Graduation = 4, Engagement = 5, Other = 6 }
public enum EventStatus { Inquiry = 1, Quoted = 2, Confirmed = 3, InProgress = 4, Completed = 5, Cancelled = 6 }
public enum EventVenue { OurHall = 1, ClientLocation = 2, Terrace = 3, Garden = 4 }

public class EventPackage : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PricePerPerson { get; set; }
    public int MinGuests { get; set; } = 20;
    public string? IncludedServices { get; set; } // "3-course menu, decoration, service staff, cake"
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class EventBooking : BaseEntity
{
    public string CustomerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public EventType Type { get; set; } = EventType.Birthday;
    public int? PackageId { get; set; }
    public EventPackage? Package { get; set; }
    public DateTime EventDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public int GuestCount { get; set; } = 50;
    public EventVenue Venue { get; set; } = EventVenue.OurHall;
    public string? Address { get; set; } // when off-site catering
    public string? MenuNotes { get; set; }
    public decimal QuoteAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public bool DepositPaid { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Inquiry;
    public string? Notes { get; set; }
    public ICollection<EventTask> Tasks { get; set; } = new List<EventTask>();
}

/// <summary>Operational checklist item for running an event (staffing, equipment, setup...).</summary>
public class EventTask : BaseEntity
{
    public int EventBookingId { get; set; }
    public EventBooking? EventBooking { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? AssignedToId { get; set; }
    public Identity.AppUser? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsDone { get; set; }
}
