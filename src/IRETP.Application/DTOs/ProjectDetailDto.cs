using IRETP.Domain.Enums;

namespace IRETP.Application.DTOs;

public class ProjectDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public Guid DeveloperId { get; set; }
    public string DeveloperName { get; set; } = default!;
    public string DeveloperNameAr { get; set; } = default!;
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public string ZoneNameAr { get; set; } = default!;
    public string? DeveloperLicenceNumber { get; set; }
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
    public List<ProjectUnitDto> Units { get; set; } = [];
    public EscrowSummaryDto? EscrowSummary { get; set; }
}

public class ProjectUnitDto
{
    public PropertyType PropertyType { get; set; }
    public int Count { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal AverageSizeSqft { get; set; }
}

public class EscrowSummaryDto
{
    public string AccountNumber { get; set; } = default!;
    public string BankName { get; set; } = default!;
    public decimal CurrentBalance { get; set; }
    public decimal RequiredMinimumBalance { get; set; }
    public decimal TotalFundsReceived { get; set; }
    public decimal TotalAuthorisedWithdrawals { get; set; }
    public decimal RemainingConstructionCost { get; set; }
    public EscrowStatus Status { get; set; }
    public decimal AdequacyRatio { get; set; }
}
