using IRETP.Domain.Entities;
using IRETP.Domain.Enums;

namespace IRETP.Infrastructure.Services.Notifications;

/// <summary>
/// Produces the HTML/plain text/SMS bodies for notifications. Keeps formatting
/// in one place so the delivery service can stay focused on dispatch.
/// </summary>
internal static class NotificationTemplates
{
    public static string BuildRiskEmailHtml(RiskAlert alert, bool isEncrypted, string? portalLink)
    {
        var header = isEncrypted ? "[ENCRYPTED] " : string.Empty;
        var levelBadge = alert.AlertLevel.ToString().Replace("_", " — ");
        var link = portalLink ?? "#";

        return $"""
        <!doctype html>
        <html lang="en">
          <body style="font-family: Arial, Helvetica, sans-serif; color:#111; margin:0; padding:24px; background:#f4f5f7;">
            <table role="presentation" width="100%" style="max-width:640px; margin:0 auto; background:#fff; border-radius:8px; overflow:hidden;">
              <tr>
                <td style="background:#0b3d91; color:#fff; padding:16px 24px;">
                  <strong>Dubai Land Department</strong> — Integrated Real Estate Transparency Platform
                </td>
              </tr>
              <tr>
                <td style="padding:24px;">
                  <h2 style="margin:0 0 8px;">{header}{WebEncode(alert.Title)}</h2>
                  <p style="margin:0 0 12px; color:#555;">Alert level: <strong>{levelBadge}</strong> · Risk: <strong>{alert.RiskLevel}</strong></p>
                  <p style="margin:0 0 16px;">{WebEncode(alert.Description)}</p>
                  <p style="margin:0 0 24px;">
                    <a href="{link}" style="background:#0b3d91; color:#fff; padding:10px 18px; text-decoration:none; border-radius:4px;">Open in IRETP</a>
                  </p>
                  <p style="margin:0; font-size:12px; color:#888;">Escalation path: {alert.EscalationPath ?? "n/a"}</p>
                </td>
              </tr>
              <tr>
                <td style="padding:16px 24px; font-size:12px; color:#888;">
                  This message is confidential. If you received it in error, please notify DLD and delete it.
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;
    }

    public static string BuildRiskEmailPlainText(RiskAlert alert, bool isEncrypted)
    {
        var prefix = isEncrypted ? "[ENCRYPTED] " : string.Empty;
        return $"""
        {prefix}{alert.Title}
        Alert level: {alert.AlertLevel} · Risk: {alert.RiskLevel}

        {alert.Description}

        Escalation path: {alert.EscalationPath ?? "n/a"}
        — Dubai Land Department IRETP
        """;
    }

    public static string BuildRiskSmsBody(RiskAlert alert)
    {
        var prefix = alert.AlertLevel switch
        {
            AlertLevel.Level4_Strategic => "DLD IRETP CRITICAL",
            AlertLevel.Level3_SeniorLeadership => "DLD IRETP HIGH",
            AlertLevel.Level2_Managerial => "DLD IRETP",
            _ => "DLD IRETP"
        };

        var body = $"{prefix}: {alert.Title}";
        return body.Length > 160 ? body[..160] : body;
    }

    public static string BuildInvestorEmailHtml(string title, string body, string? portalLink, string? unsubscribeUrl = null)
    {
        var link = portalLink ?? "#";
        var unsubHref = unsubscribeUrl ?? "#";
        var unsubPara = string.IsNullOrEmpty(unsubscribeUrl)
            ? "You are receiving this because you configured a DLD IRETP market alert. Manage or unsubscribe from your account settings."
            : $"""
               You are receiving this because you configured a DLD IRETP market alert.
               <a href="{unsubHref}" style="color:#0b3d91;">Unsubscribe</a> or manage preferences in your account settings.
               """;

        return $"""
        <!doctype html>
        <html lang="en">
          <body style="font-family: Arial, Helvetica, sans-serif; color:#111; margin:0; padding:24px; background:#f4f5f7;">
            <table role="presentation" width="100%" style="max-width:640px; margin:0 auto; background:#fff; border-radius:8px; overflow:hidden;">
              <tr>
                <td style="background:#0b3d91; color:#fff; padding:16px 24px;">
                  <strong>Dubai Land Department</strong> — Market Alert
                </td>
              </tr>
              <tr>
                <td style="padding:24px;">
                  <h2 style="margin:0 0 12px;">{WebEncode(title)}</h2>
                  <p style="margin:0 0 24px;">{WebEncode(body)}</p>
                  <p style="margin:0;">
                    <a href="{link}" style="background:#0b3d91; color:#fff; padding:10px 18px; text-decoration:none; border-radius:4px;">View on IRETP</a>
                  </p>
                </td>
              </tr>
              <tr>
                <td style="padding:16px 24px; font-size:12px; color:#888;">
                  {unsubPara}
                </td>
              </tr>
            </table>
          </body>
        </html>
        """;
    }

    private static string WebEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
