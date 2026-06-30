using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

/// <summary>
/// Green-building certification awarded to an IRETP project — backs the
/// public ESG/Sustainability module and the map heatmap layer called out in
/// RFP Section 20. One project may carry several certifications over its
/// lifecycle; the dashboard picks the highest active level per project.
/// </summary>
public class ProjectCertification : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = default!;

    public CertificationScheme Scheme { get; set; }
    public CertificationLevel Level { get; set; }

    /// <summary>
    /// Optional reference number issued by the awarding body (e.g. the
    /// USGBC project ID for LEED). Displayed on the public project page for
    /// verifiability — required by the Transparency sub-index.
    /// </summary>
    public string? CertificateNumber { get; set; }

    public DateTime AwardedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Score normalised to a 0–100 scale by the awarding body where one is
    /// available; null when the scheme uses discrete levels only.
    /// </summary>
    public decimal? ScorePct { get; set; }
}
