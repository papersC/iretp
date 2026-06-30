using IRETP.Domain.Enums;

namespace IRETP.Application.Interfaces;

/// <summary>
/// Resolves the concrete recipients for EWRS escalations (Section 8.2). Each
/// AlertLevel maps to a set of DLD roles; this service returns the current
/// users holding those roles with their contact details.
/// </summary>
public interface INotificationRecipientResolver
{
    Task<IReadOnlyList<NotificationRecipient>> ResolveForEwrsAsync(AlertLevel alertLevel, CancellationToken ct = default);

    Task<NotificationRecipient?> ResolveByUserIdAsync(string userId, CancellationToken ct = default);
}

public sealed record NotificationRecipient(
    string UserId,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    string PreferredLanguage);
