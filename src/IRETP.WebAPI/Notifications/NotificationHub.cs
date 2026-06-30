using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IRETP.WebAPI.Notifications;

/// <summary>
/// SignalR hub at <c>/hubs/notifications</c>. Each connection joins a group
/// named after the authenticated user's id so targeted broadcasts hit only
/// the intended browser session. Anonymous connections are permitted but
/// never receive user-specific messages.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public const string Path = "/hubs/notifications";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, UserGroup(userId));
        }
        await base.OnDisconnectedAsync(exception);
    }

    public static string UserGroup(string userId) => $"user:{userId}";
}
