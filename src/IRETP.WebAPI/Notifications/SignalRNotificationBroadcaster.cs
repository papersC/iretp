using IRETP.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace IRETP.WebAPI.Notifications;

/// <summary>
/// Pushes notification events into the SignalR hub. Registered by WebAPI's
/// Program.cs to replace the Infrastructure NoOp implementation.
/// </summary>
public sealed class SignalRNotificationBroadcaster : INotificationBroadcaster
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationBroadcaster(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task BroadcastAsync(string userId, NotificationBroadcastPayload payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Task.CompletedTask;

        // Method name matches the frontend SignalR client contract.
        return _hub.Clients
            .Group(NotificationHub.UserGroup(userId))
            .SendAsync("notification", payload, ct);
    }
}
