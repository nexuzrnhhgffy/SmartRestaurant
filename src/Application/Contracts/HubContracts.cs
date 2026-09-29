using SmartRestaurant.Application.Common;
using SmartRestaurant.Domain.Common;
using SmartRestaurant.Domain.Entities;

namespace SmartRestaurant.Application.Contracts;

/// <summary>SignalR push contract for the Kitchen Display System (server → clients).</summary>
public interface IKdsClient
{
    Task TicketCreated(KdsTicketDto ticket);
    Task TicketUpdated(KdsTicketDto ticket);
    Task TicketRemoved(Guid orderItemId);
    Task OrderStatusChanged(Guid orderId, string orderNumber, string status);
}

/// <summary>General notifications push (server → clients).</summary>
public interface IAppClient
{
    Task ReceiveNotification(NotificationDto notification);
    Task OrderUpdated(OrderDto order);
    Task StatsRefresh(string reason);
    Task PaymentCaptured(PaymentDto payment);
}
