using IRETP.Application.Interfaces;

namespace IRETP.Infrastructure.Services.Notifications;

/// <summary>
/// Default implementation registered by <see cref="DependencyInjection"/>.
/// Keeps the Infrastructure project free of a SignalR dependency — the
/// WebAPI process swaps this for <c>SignalRNotificationBroadcaster</c> at
/// startup to light up real-time push.
/// </summary>
internal sealed class NoOpNotificationBroadcaster : INotificationBroadcaster
{
    public Task BroadcastAsync(string userId, NotificationBroadcastPayload payload, CancellationToken ct = default)
        => Task.CompletedTask;
}
