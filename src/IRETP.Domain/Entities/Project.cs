using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class Project : BaseEntity
{
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public Guid DeveloperId { get; set; }
    public Developer Developer { get; set; } = default!;
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = default!;
    public ProjectStatus Status { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int TotalUnits { get; set; }
    public int SoldUnits { get; set; }
    public int AvailableUnits { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? DldRegistrationNumber { get; set; }
    public decimal? TotalProjectCost { get; set; }

    public EscrowAccount? EscrowAccount { get; set; }
    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<ProjectUnit> Units { get; set; } = [];
    public ICollection<ProjectCertification> Certifications { get; set; } = [];
}
