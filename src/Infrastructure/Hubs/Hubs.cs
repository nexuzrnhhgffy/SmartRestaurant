using Microsoft.AspNetCore.SignalR;
using SmartRestaurant.Application.Contracts;

namespace SmartRestaurant.Infrastructure.Hubs;

/// <summary>Kitchen Display System channel. Clients join branch groups: "branch:{id}".</summary>
public class KdsHub : Hub<IKdsClient>
{
    public Task JoinBranch(string branchId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}");
    public Task LeaveBranch(string branchId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"branch:{branchId}");
}

/// <summary>General notifications / order stream. Groups: branch:{id}, role:{n}, user:{id}.</summary>
public class NotificationHub : Hub<IAppClient>
{
    public Task JoinBranch(string branchId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"branch:{branchId}");
    public Task JoinRole(int role) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");
    public Task JoinUser(string userId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
}
