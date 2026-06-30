namespace IRETP.Application.Interfaces;

/// <summary>
/// Dispatches SMS via the configured gateway. RFP Section 6.2 requires delivery
/// within 3 minutes of trigger and payload under 160 characters.
/// </summary>
public interface ISmsSender
{
    Task<SmsDeliveryResult> SendAsync(SmsMessage message, CancellationToken ct = default);
}

public sealed record SmsMessage(string ToPhoneNumber, string Body);

public sealed record SmsDeliveryResult(bool Success, string? ProviderMessageId, string? ErrorMessage);
