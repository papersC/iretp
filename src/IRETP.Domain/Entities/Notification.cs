using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class Notification : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string TitleAr { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string MessageAr { get; set; } = default!;
    public string? Link { get; set; }
    public string Channel { get; set; } = default!; // InPlatform, Email, SMS
    public string? Category { get; set; } // Price, Project, Watchlist, Yield, Digest, Regulation, Risk
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? SentAt { get; set; }
    public bool IsSent { get; set; }

    /// <summary>
    /// Provider message id returned on successful dispatch (SMTP message-id,
    /// SMS provider id). Null for in-platform notifications.
    /// </summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>
    /// Populated when a delivery attempt fails; used for retries and incident
    /// reporting per RFP Section 15.3.
    /// </summary>
    public string? DeliveryError { get; set; }

    public int DeliveryAttempts { get; set; }
}
