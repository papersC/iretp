using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

/// <summary>
/// Publicly disclosable ultimate beneficial ownership of a registered
/// developer (RFP Section 20 — flagged by JLL's GRETI 2024 as a key UAE
/// transparency gap). Stored as discrete rows so the sum of
/// <see cref="OwnershipPct"/> across a developer is the total disclosed
/// share (may be below 100% where lower-threshold owners are exempt).
/// </summary>
public class BeneficialOwner : BaseEntity
{
    public Guid DeveloperId { get; set; }
    public Developer Developer { get; set; } = default!;

    public string OwnerName { get; set; } = default!;
    public string? OwnerNameAr { get; set; }

    /// <summary>
    /// "Individual", "Corporate", "SovereignFund", "Foundation".
    /// </summary>
    public string OwnerType { get; set; } = "Individual";

    public string? CountryOfIncorporation { get; set; }   // ISO 2-letter
    public decimal OwnershipPct { get; set; }
    public DateTime DisclosedAt { get; set; }
    public string? DisclosureSource { get; set; }          // e.g. "RERA annual return 2025-Q4"
    public string? Notes { get; set; }
}
