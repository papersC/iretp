using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class Developer : BaseEntity
{
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string LicenceNumber { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<DeveloperScore> Scores { get; set; } = [];
    public ICollection<RegulatoryViolation> Violations { get; set; } = [];
    public ICollection<BeneficialOwner> BeneficialOwners { get; set; } = [];
}
