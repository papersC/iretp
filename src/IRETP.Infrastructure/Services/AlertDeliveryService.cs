using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using IRETP.Infrastructure.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Processes new EWRS RiskAlerts and pending Notification rows, dispatches them
/// through the configured email / SMS senders, and tracks per-channel
/// delivery status (RFP Section 6.2 SLA, Section 8.2 escalation framework).
/// </summary>
public class AlertDeliveryService
{
    private const int MaxDeliveryAttempts = 3;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertDeliveryService> _logger;

    public AlertDeliveryService(IServiceScopeFactory scopeFactory, ILogger<AlertDeliveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Full pass: materialise notifications for any new RiskAlerts, then
    /// attempt external dispatch (email + SMS) for any notifications still
    /// pending. Designed to be called on a short recurring schedule.
    /// </summary>
    public async Task DeliverPendingAlertsAsync()
    {
        await MaterialiseEwrsNotificationsAsync();
        await DispatchPendingNotificationsAsync();
    }

    // -----------------------------------------------------------------------
    // Step 1 — turn new RiskAlerts into per-recipient Notification rows
    // -----------------------------------------------------------------------
    private async Task MaterialiseEwrsNotificationsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var alertRepo = scope.ServiceProvider.GetRequiredService<IRepository<RiskAlert>>();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<IRepository<Notification>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var resolver = scope.ServiceProvider.GetRequiredService<INotificationRecipientResolver>();

        var pendingAlerts = await alertRepo.Query()
            .Where(a => a.Status == AlertStatus.New)
            .ToListAsync();

        if (pendingAlerts.Count == 0) return;

        // Avoid re-emitting notifications for the same RiskAlert on every poll.
        // We compare against the alert's "epoch" — the later of CreatedAt and
        // LastEscalatedAt. If a notification already exists for this alert at
        // this epoch, skip it; otherwise re-emit so newly-eligible recipients
        // (after an auto-escalation) actually receive the alert.
        var alertIdsPattern = pendingAlerts.Select(a => $"/admin/ewrs/alerts/{a.Id}").ToList();
        var existingForAlerts = await notificationRepo.Query()
            .Where(n => n.Link != null && alertIdsPattern.Contains(n.Link))
            .Select(n => new { n.Link, n.CreatedAt })
            .ToListAsync();
        var latestNotificationByLink = existingForAlerts
            .GroupBy(n => n.Link!)
            .ToDictionary(g => g.Key, g => g.Max(x => x.CreatedAt));

        var toAdd = new List<Notification>();

        foreach (var alert in pendingAlerts)
        {
            var link = $"/admin/ewrs/alerts/{alert.Id}";
            var epoch = alert.LastEscalatedAt ?? alert.CreatedAt;
            if (latestNotificationByLink.TryGetValue(link, out var lastNotifiedAt) && lastNotifiedAt >= epoch)
                continue;

            var recipients = await resolver.ResolveForEwrsAsync(alert.AlertLevel);
            if (recipients.Count == 0)
            {
                // Persist a system-addressed InApp record so the alert is at least visible in the audit log
                toAdd.Add(BuildRiskNotification(alert, channel: "InApp", userId: "system", link: link));
                continue;
            }

            foreach (var recipient in recipients)
            {
                toAdd.Add(BuildRiskNotification(alert, "InApp", recipient.UserId, link));

                if (alert.AlertLevel >= AlertLevel.Level2_Managerial && !string.IsNullOrEmpty(recipient.Email))
                {
                    toAdd.Add(BuildRiskNotification(alert, "Email", recipient.UserId, link,
                        isEncrypted: alert.AlertLevel == AlertLevel.Level4_Strategic));
                }

                if (alert.AlertLevel >= AlertLevel.Level3_SeniorLeadership && !string.IsNullOrEmpty(recipient.PhoneNumber))
                {
                    toAdd.Add(BuildRiskNotification(alert, "SMS", recipient.UserId, link));
                }
            }

            _logger.LogInformation(
                "EWRS alert {AlertId} ({AlertLevel}) queued for {RecipientCount} recipient(s)",
                alert.Id, alert.AlertLevel, recipients.Count);
        }

        if (toAdd.Count > 0)
        {
            await notificationRepo.AddRangeAsync(toAdd);
            await unitOfWork.SaveChangesAsync();

            // Push the in-app rows so connected clients refresh immediately.
            // Email/SMS rows push on successful external dispatch instead —
            // avoids racing the user-visible state ahead of actual delivery.
            var broadcaster = scope.ServiceProvider.GetService<IRETP.Application.Interfaces.INotificationBroadcaster>();
            if (broadcaster is not null)
            {
                foreach (var note in toAdd.Where(n => n.Channel == "InApp" && n.UserId != "system"))
                {
                    await broadcaster.BroadcastAsync(note.UserId,
                        new IRETP.Application.Interfaces.NotificationBroadcastPayload(
                            note.Title, note.Message, note.Category, note.Link, DateTime.UtcNow));
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // Step 2 — actually send the Email / SMS notifications
    // -----------------------------------------------------------------------
    private async Task DispatchPendingNotificationsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<IRepository<Notification>>();
        var alertRepo = scope.ServiceProvider.GetRequiredService<IRepository<RiskAlert>>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var smsSender = scope.ServiceProvider.GetRequiredService<ISmsSender>();
        var resolver = scope.ServiceProvider.GetRequiredService<INotificationRecipientResolver>();
        var unsubscribeTokens = scope.ServiceProvider.GetRequiredService<IUnsubscribeTokenService>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>().Value;
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        // In-platform records never need external delivery — mark them sent on first pass
        var inAppPending = await notificationRepo.Query()
            .Where(n => !n.IsSent && n.Channel == "InApp")
            .ToListAsync();

        foreach (var note in inAppPending)
        {
            note.IsSent = true;
            note.SentAt = DateTime.UtcNow;
        }

        var externalPending = await notificationRepo.Query()
            .Where(n => !n.IsSent
                        && (n.Channel == "Email" || n.Channel == "SMS")
                        && n.DeliveryAttempts < MaxDeliveryAttempts)
            .Take(200) // cap per pass; dispatcher runs frequently
            .ToListAsync();

        if (externalPending.Count == 0 && inAppPending.Count == 0)
        {
            await unitOfWork.SaveChangesAsync();
            return;
        }

        // Pre-load RiskAlert context for notifications that point at /admin/ewrs/alerts/{id}
        var linkedAlertIds = externalPending
            .Where(n => n.Link != null && n.Link.StartsWith("/admin/ewrs/alerts/"))
            .Select(n => Guid.TryParse(n.Link!["/admin/ewrs/alerts/".Length..], out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var riskAlertsById = linkedAlertIds.Count == 0
            ? new Dictionary<Guid, RiskAlert>()
            : (await alertRepo.Query()
                .Where(a => linkedAlertIds.Contains(a.Id))
                .ToListAsync())
                .ToDictionary(a => a.Id);

        foreach (var note in externalPending)
        {
            note.DeliveryAttempts++;
            var recipient = await resolver.ResolveByUserIdAsync(note.UserId);
            if (recipient is null)
            {
                note.DeliveryError = $"Recipient '{note.UserId}' could not be resolved.";
                continue;
            }

            var riskAlert = TryGetLinkedAlert(note, riskAlertsById);

            if (note.Channel == "Email")
            {
                // RFP §6.2: investor-facing emails carry a one-click
                // unsubscribe link keyed by (userId, alertCategory). EWRS
                // operational alerts to DLD staff omit it — those are
                // non-optional.
                string? unsubscribeUrl = null;
                if (riskAlert is null && note.UserId is { Length: > 0 } uid)
                {
                    var reason = note.Category ?? "marketing";
                    var token = unsubscribeTokens.Mint(uid, reason);
                    var baseUrl = options.PortalBaseUrl?.TrimEnd('/') ?? string.Empty;
                    unsubscribeUrl = $"{baseUrl}/api/account/unsubscribe?u={Uri.EscapeDataString(uid)}&r={Uri.EscapeDataString(reason)}&t={token}";
                }
                await DispatchEmailAsync(emailSender, note, recipient, riskAlert, unsubscribeUrl);
            }
            else if (note.Channel == "SMS")
            {
                await DispatchSmsAsync(smsSender, note, recipient, riskAlert);
            }
        }

        await unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Notification dispatcher pass: {InApp} in-app marked sent, {External} external attempts made.",
            inAppPending.Count, externalPending.Count);
    }

    private static async Task DispatchEmailAsync(
        IEmailSender sender, Notification note, NotificationRecipient recipient, RiskAlert? riskAlert,
        string? unsubscribeUrl = null)
    {
        var isEncrypted = note.Title.StartsWith("[ENCRYPTED]", StringComparison.Ordinal);
        string html, plain;

        if (riskAlert is not null)
        {
            html = NotificationTemplates.BuildRiskEmailHtml(riskAlert, isEncrypted, note.Link);
            plain = NotificationTemplates.BuildRiskEmailPlainText(riskAlert, isEncrypted);
        }
        else
        {
            html = NotificationTemplates.BuildInvestorEmailHtml(note.Title, note.Message, note.Link, unsubscribeUrl);
            plain = $"{note.Title}\n\n{note.Message}";
            if (!string.IsNullOrEmpty(unsubscribeUrl))
            {
                plain += $"\n\nUnsubscribe: {unsubscribeUrl}";
            }
        }

        var result = await sender.SendAsync(new EmailMessage(
            ToAddress: recipient.Email ?? string.Empty,
            ToName: recipient.DisplayName,
            Subject: note.Title,
            HtmlBody: html,
            PlainTextBody: plain,
            IsEncrypted: isEncrypted,
            UnsubscribeUrl: unsubscribeUrl));

        if (result.Success)
        {
            note.IsSent = true;
            note.SentAt = DateTime.UtcNow;
            note.ProviderMessageId = result.ProviderMessageId;
            note.DeliveryError = null;
        }
        else
        {
            note.DeliveryError = result.ErrorMessage;
        }
    }

    private static async Task DispatchSmsAsync(
        ISmsSender sender, Notification note, NotificationRecipient recipient, RiskAlert? riskAlert)
    {
        var body = riskAlert is not null
            ? NotificationTemplates.BuildRiskSmsBody(riskAlert)
            : TrimSms(note.Title);

        var result = await sender.SendAsync(new SmsMessage(
            ToPhoneNumber: recipient.PhoneNumber ?? string.Empty,
            Body: body));

        if (result.Success)
        {
            note.IsSent = true;
            note.SentAt = DateTime.UtcNow;
            note.ProviderMessageId = result.ProviderMessageId;
            note.DeliveryError = null;
        }
        else
        {
            note.DeliveryError = result.ErrorMessage;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static Notification BuildRiskNotification(
        RiskAlert alert, string channel, string userId, string link, bool isEncrypted = false)
    {
        var title = isEncrypted ? $"[ENCRYPTED] Risk Alert: {alert.Title}" : $"Risk Alert: {alert.Title}";
        var titleAr = isEncrypted ? $"[مشفر] تنبيه مخاطر: {alert.Title}" : $"تنبيه مخاطر: {alert.Title}";

        return new Notification
        {
            UserId = userId,
            Title = title,
            TitleAr = titleAr,
            Message = alert.Description,
            MessageAr = alert.Description,
            Link = link,
            Channel = channel,
            Category = "Risk",
            IsRead = false,
            IsSent = false,
            DeliveryAttempts = 0
        };
    }

    private static RiskAlert? TryGetLinkedAlert(Notification note, IReadOnlyDictionary<Guid, RiskAlert> index)
    {
        if (note.Link is null || !note.Link.StartsWith("/admin/ewrs/alerts/")) return null;
        return Guid.TryParse(note.Link["/admin/ewrs/alerts/".Length..], out var id) && index.TryGetValue(id, out var a)
            ? a : null;
    }

    private static string TrimSms(string value) =>
        value.Length > 160 ? value[..160] : value;
}
