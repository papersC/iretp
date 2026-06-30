namespace IRETP.Application.Interfaces;

/// <summary>
/// Dispatches transactional email via the configured provider (SMTP, SendGrid,
/// etc.). Implementations must respect the RFP Section 6.2 SLA — delivery within
/// 5 minutes of trigger — and log every send attempt to the audit trail.
/// </summary>
public interface IEmailSender
{
    Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken ct = default);
}

public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null,
    bool IsEncrypted = false,
    // RFP §6.2: every market-alert / digest email must expose a one-click
    // unsubscribe mechanism. Populate with a user-specific URL pointing at
    // /account/unsubscribe or an RFC 8058 List-Unsubscribe-Post endpoint;
    // null for operational notifications that don't require opt-out (e.g.
    // password reset, MFA codes, DLD internal EWRS alerts).
    string? UnsubscribeUrl = null);

public sealed record EmailDeliveryResult(bool Success, string? ProviderMessageId, string? ErrorMessage);
