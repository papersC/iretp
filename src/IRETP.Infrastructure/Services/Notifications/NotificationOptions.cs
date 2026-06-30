namespace IRETP.Infrastructure.Services.Notifications;

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    public EmailOptions Email { get; set; } = new();
    public SmsOptions Sms { get; set; } = new();

    /// <summary>
    /// Public base URL for the WebAPI. Used when minting absolute
    /// unsubscribe URLs that land on <c>/api/account/unsubscribe</c>. In
    /// production this is the DLD public domain; in development it defaults
    /// to <c>http://localhost:5000</c>.
    /// </summary>
    public string PortalBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// Secret used by <c>HmacUnsubscribeTokenService</c>. Must be rotated on
    /// compromise; rotating invalidates every outstanding unsubscribe link.
    /// </summary>
    public string? UnsubscribeSecret { get; set; }
}

public class EmailOptions
{
    /// <summary>
    /// "Smtp" (default) uses the configured SMTP host. "Log" writes to the
    /// logger only — used for local development or when provider credentials
    /// are not yet provisioned.
    /// </summary>
    public string Provider { get; set; } = "Log";

    public string FromAddress { get; set; } = "no-reply@iretp.dld.gov.ae";
    public string FromName { get; set; } = "Dubai Land Department — IRETP";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class SmsOptions
{
    /// <summary>
    /// "Twilio" uses the Twilio REST API. "Log" writes to the logger only —
    /// used for local development or when a gateway has not been provisioned.
    /// </summary>
    public string Provider { get; set; } = "Log";

    public string FromNumber { get; set; } = "IRETP";
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }

    /// <summary>
    /// Some regional gateways expose a plain HTTPS POST endpoint. When set,
    /// overrides the Twilio path and the sender POSTs JSON { to, from, body }.
    /// </summary>
    public string? GatewayUrl { get; set; }
    public string? GatewayApiKey { get; set; }
}
