using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class SavedAnalyticsView : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string ConfigurationJson { get; set; } = default!; // Stores dimensions, metrics, filters, chart type
    public bool IsPublic { get; set; }
    public string? ShareToken { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>
    /// RFP AN-006: shareable analysis links must be valid for a minimum of
    /// 12 months. Stamped the first time the view is marked public.
    /// </summary>
    public DateTime? ShareTokenExpiresAt { get; set; }
}
