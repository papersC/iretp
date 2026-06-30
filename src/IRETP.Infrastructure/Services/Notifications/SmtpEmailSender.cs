using System.Net;
using System.Net.Mail;
using IRETP.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IRETP.Infrastructure.Services.Notifications;

/// <summary>
/// SMTP implementation of IEmailSender. Falls back to a log-only mode when the
/// provider is set to "Log" so developers can run the platform without a real
/// SMTP server. Production configuration uses the DLD-approved relay; the
/// provider name is kept in config (RFP 11.1 — no specific product mandated).
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<NotificationOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value.Email;
        _logger = logger;
    }

    public async Task<EmailDeliveryResult> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message.ToAddress))
        {
            return new EmailDeliveryResult(false, null, "Recipient email address is missing.");
        }

        if (string.Equals(_options.Provider, "Log", StringComparison.OrdinalIgnoreCase))
        {
            var simulatedId = $"log-{Guid.NewGuid():N}";
            _logger.LogInformation(
                "[EMAIL:LOG] to={To} subject={Subject} encrypted={Encrypted} messageId={MessageId}",
                message.ToAddress, message.Subject, message.IsEncrypted, simulatedId);
            return new EmailDeliveryResult(true, simulatedId, null);
        }

        try
        {
            using var smtp = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = string.IsNullOrEmpty(_options.Username)
            };

            if (!string.IsNullOrEmpty(_options.Username))
            {
                smtp.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(new MailAddress(message.ToAddress, message.ToName));

            if (!string.IsNullOrEmpty(message.PlainTextBody))
            {
                mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                    message.PlainTextBody, null, "text/plain"));
            }

            // RFC 8058 one-click unsubscribe. Mail clients + anti-spam filters
            // respect this to short-circuit the deliverability complaint loop.
            if (!string.IsNullOrEmpty(message.UnsubscribeUrl))
            {
                mail.Headers["List-Unsubscribe"] = $"<{message.UnsubscribeUrl}>";
                mail.Headers["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click";
            }

            // SmtpClient.SendMailAsync does not accept a CT directly; honour cancellation via Task.Run
            await smtp.SendMailAsync(mail, ct);

            var providerId = mail.Headers["Message-ID"] ?? $"smtp-{Guid.NewGuid():N}";
            _logger.LogInformation(
                "Email sent via SMTP to {To} (subject={Subject}, messageId={MessageId})",
                message.ToAddress, message.Subject, providerId);
            return new EmailDeliveryResult(true, providerId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed for {To} (subject={Subject})",
                message.ToAddress, message.Subject);
            return new EmailDeliveryResult(false, null, ex.Message);
        }
    }
}
