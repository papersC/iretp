using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class RegulatoryViolation : BaseEntity
{
    public Guid DeveloperId { get; set; }
    public Developer Developer { get; set; } = default!;
    public DateTime ViolationDate { get; set; }
    public ViolationSeverity Severity { get; set; }
    public string Description { get; set; } = default!;
    public string? DescriptionAr { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? IssuedBy { get; set; }
}
