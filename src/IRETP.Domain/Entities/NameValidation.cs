using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

/// <summary>
/// Tracks the Arabic-name validation workflow (RFP FR009 acceptance — Arabic
/// zone, project, and developer names must be validated against DLD official
/// records before go-live). One row per entity name; rows transition through
/// <see cref="NameValidationStatus"/> as reviewers confirm or reject.
/// </summary>
public class NameValidation : BaseEntity
{
    /// <summary>"Zone", "Project", "Developer".</summary>
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }

    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;

    /// <summary>Authoritative value from DLD records — blank until reviewer pastes it in.</summary>
    public string? OfficialNameAr { get; set; }

    public NameValidationStatus Status { get; set; } = NameValidationStatus.Pending;

    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}

public enum NameValidationStatus
{
    Pending = 0,
    Validated = 1,
    Rejected = 2,
    NeedsCorrection = 3
}
