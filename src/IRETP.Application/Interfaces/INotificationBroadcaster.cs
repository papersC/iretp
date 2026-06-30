namespace IRETP.Application.Interfaces;

/// <summary>
/// Notifies connected clients in real time that a user has new notifications
/// waiting. RFP Section 6.2 requires in-platform notifications to be
/// instantaneous — polling satisfies the SLA, but pushing via this broadcaster
/// eliminates the polling window and reduces backend load.
/// </summary>
public interface INotificationBroadcaster
{
    Task BroadcastAsync(string userId, NotificationBroadcastPayload payload, CancellationToken ct = default);
}

public sealed record NotificationBroadcastPayload(
    string Title,
    string? Message,
    string? Category,
    string? Link,
    DateTime CreatedAt);
