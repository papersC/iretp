using IRETP.Application.Interfaces;
using IRETP.Domain.Enums;
using IRETP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services.Notifications;

/// <summary>
/// Maps EWRS AlertLevel (RFP Section 8.2) to concrete DLD users by role and
/// hydrates their contact details. Cumulative escalation: a Level 3 alert goes
/// to Level 1, 2, and 3 recipients; a Level 4 alert goes to everyone.
/// </summary>
public class NotificationRecipientResolver : INotificationRecipientResolver
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<NotificationRecipientResolver> _logger;

    public NotificationRecipientResolver(
        UserManager<ApplicationUser> userManager,
        ILogger<NotificationRecipientResolver> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationRecipient>> ResolveForEwrsAsync(
        AlertLevel alertLevel, CancellationToken ct = default)
    {
        var roles = RolesForLevel(alertLevel);
        var recipients = new Dictionary<string, NotificationRecipient>(StringComparer.Ordinal);

        foreach (var role in roles)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            foreach (var user in users.Where(u => u.IsInternalUser))
            {
                recipients[user.Id] = Map(user);
            }
        }

        if (recipients.Count == 0)
        {
            _logger.LogWarning(
                "No internal users found for EWRS AlertLevel {Level}; alert will be persisted but not delivered externally.",
                alertLevel);
        }

        return recipients.Values.ToList();
    }

    public async Task<NotificationRecipient?> ResolveByUserIdAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId == "system") return null;
        var user = await _userManager.FindByIdAsync(userId);
        return user is null ? null : Map(user);
    }

    private static NotificationRecipient Map(ApplicationUser user) => new(
        UserId: user.Id,
        DisplayName: $"{user.FirstName} {user.LastName}".Trim(),
        Email: user.Email,
        PhoneNumber: user.PhoneNumber,
        PreferredLanguage: string.IsNullOrWhiteSpace(user.PreferredLanguage) ? "en" : user.PreferredLanguage);

    /// <summary>
    /// Role → AlertLevel mapping derived from RFP Section 8.2. The escalation
    /// is cumulative so that lower tiers remain informed of high-level
    /// incidents.
    /// </summary>
    private static IEnumerable<string> RolesForLevel(AlertLevel alertLevel) => alertLevel switch
    {
        AlertLevel.Level1_Operational =>
            new[] { UserRoles.DldOperator },

        AlertLevel.Level2_Managerial =>
            new[] { UserRoles.DldOperator, UserRoles.DldSupervisor },

        AlertLevel.Level3_SeniorLeadership =>
            new[] { UserRoles.DldOperator, UserRoles.DldSupervisor, UserRoles.SystemAdministrator },

        AlertLevel.Level4_Strategic =>
            new[]
            {
                UserRoles.DldOperator,
                UserRoles.DldSupervisor,
                UserRoles.SystemAdministrator,
                UserRoles.DldViewer
            },

        _ => Array.Empty<string>()
    };
}
