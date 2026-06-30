using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

/// <summary>
/// Immutable snapshot of a <see cref="CmsContent"/> at each draft or publish
/// event. Backs the FR002 requirement for 12-month version retention with
/// rollback capability (RFP Section 3.1). One row per version; rollback
/// creates a new row rather than mutating history.
/// </summary>
public class CmsContentVersion : BaseEntity
{
    public Guid CmsContentId { get; set; }
    public CmsContent CmsContent { get; set; } = default!;

    public int VersionNumber { get; set; }

    /// <summary>"Draft", "Published", "Rollback".</summary>
    public string ChangeType { get; set; } = "Draft";

    public string ContentType { get; set; } = default!;
    public string ContentEn { get; set; } = default!;
    public string ContentAr { get; set; } = default!;
    public int SortOrder { get; set; }

    public string? Summary { get; set; }

    /// <summary>
    /// UTC-seeded random token enabling DLD leadership to preview a draft via
    /// a shareable URL (FR002). Null on Published rows — the live content is
    /// already public.
    /// </summary>
    public string? PreviewToken { get; set; }
    public DateTime? PreviewTokenExpiresAt { get; set; }
}
