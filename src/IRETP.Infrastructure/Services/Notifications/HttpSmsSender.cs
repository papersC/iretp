using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using IRETP.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IRETP.Infrastructure.Services.Notifications;

/// <summary>
/// SMS sender that supports two providers without taking a hard dependency on
/// Twilio's SDK: (a) Twilio's REST API via HTTP Basic auth, and (b) a generic
/// UAE gateway that accepts JSON { to, from, body }. A "Log" provider is
/// supported for local development. The 160-character payload cap from RFP
/// Section 6.2 is enforced here.
/// </summary>
public class HttpSmsSender : ISmsSender
{
    private const int MaxSmsLength = 160;
    private readonly SmsOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpSmsSender> _logger;

    public HttpSmsSender(
        IOptions<NotificationOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<HttpSmsSender> logger)
    {
        _options = options.Value.Sms;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SmsDeliveryResult> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message.ToPhoneNumber))
        {
            return new SmsDeliveryResult(false, null, "Recipient phone number is missing.");
        }

        var body = message.Body.Length > MaxSmsLength
            ? message.Body[..MaxSmsLength]
            : message.Body;

        if (string.Equals(_options.Provider, "Log", StringComparison.OrdinalIgnoreCase))
        {
            var simulatedId = $"log-{Guid.NewGuid():N}";
            _logger.LogInformation("[SMS:LOG] to={To} body={Body} messageId={MessageId}",
                message.ToPhoneNumber, body, simulatedId);
            return new SmsDeliveryResult(true, simulatedId, null);
        }

        try
        {
            if (!string.IsNullOrEmpty(_options.GatewayUrl))
            {
                return await SendViaGenericGatewayAsync(message.ToPhoneNumber, body, ct);
            }

            return await SendViaTwilioAsync(message.ToPhoneNumber, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS send failed for {To}", message.ToPhoneNumber);
            return new SmsDeliveryResult(false, null, ex.Message);
        }
    }

    private async Task<SmsDeliveryResult> SendViaTwilioAsync(string to, string body, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_options.AccountSid) || string.IsNullOrEmpty(_options.AuthToken))
        {
            return new SmsDeliveryResult(false, null, "Twilio credentials are not configured.");
        }

        var client = _httpClientFactory.CreateClient("SmsGateway");
        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_options.AccountSid}/Messages.json";

        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("From", _options.FromNumber),
            new KeyValuePair<string, string>("To", to),
            new KeyValuePair<string, string>("Body", body)
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = form };
        var basic = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new SmsDeliveryResult(false, null, $"Twilio {(int)response.StatusCode}: {payload}");
        }

        _logger.LogInformation("SMS sent via Twilio to {To}", to);
        return new SmsDeliveryResult(true, ExtractTwilioSid(payload), null);
    }

    private async Task<SmsDeliveryResult> SendViaGenericGatewayAsync(string to, string body, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("SmsGateway");

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.GatewayUrl)
        {
            Content = JsonContent.Create(new { to, from = _options.FromNumber, body })
        };

        if (!string.IsNullOrEmpty(_options.GatewayApiKey))
        {
            request.Headers.Add("X-Api-Key", _options.GatewayApiKey);
        }

        var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            return new SmsDeliveryResult(false, null, $"Gateway {(int)response.StatusCode}: {payload}");
        }

        _logger.LogInformation("SMS sent via gateway to {To}", to);
        return new SmsDeliveryResult(true, $"gw-{Guid.NewGuid():N}", null);
    }

    private static string? ExtractTwilioSid(string json)
    {
        const string key = "\"sid\":\"";
        var start = json.IndexOf(key, StringComparison.Ordinal);
        if (start < 0) return null;
        start += key.Length;
        var end = json.IndexOf('"', start);
        return end > start ? json[start..end] : null;
    }
}
