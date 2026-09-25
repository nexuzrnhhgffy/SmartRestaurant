using SmartRestaurant.Domain.Entities.Common;

namespace SmartRestaurant.Domain.Entities.Notifications;

public enum NotificationType { NewOrder = 1, LowStock = 2, Reservation = 3, EventInquiry = 4, Review = 5, System = 6, Payment = 7 }

public class Notification : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.System;
    public Domain.Entities.Identity.AppRole? TargetRole { get; set; } // null = broadcast
    public bool IsRead { get; set; }
    public string? Link { get; set; } // deep link in dashboard
}
