using IRETP.Domain.Enums;

namespace IRETP.Application.DTOs;

public class DeveloperProfileDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string LicenceNumber { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? Website { get; set; }
    public bool IsActive { get; set; }
    public List<DeveloperProjectDto> Projects { get; set; } = [];
    public List<DeveloperScoreHistoryDto> ScoreHistory { get; set; } = [];
    public List<ViolationDto> Violations { get; set; } = [];
    public int TotalUnitsDelivered { get; set; }
    public int TotalProjectsCompleted { get; set; }
    public decimal? LatestCompositeScore { get; set; }
}

public class DeveloperProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string ZoneName { get; set; } = default!;
    public ProjectStatus Status { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int TotalUnits { get; set; }
    public decimal? EscrowBalance { get; set; }
    public EscrowStatus? EscrowStatus { get; set; }
}

public class DeveloperScoreHistoryDto
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal CompositeScore { get; set; }
    public decimal OnTimeDeliveryScore { get; set; }
    public decimal UnitSalesCompletionScore { get; set; }
    public decimal EscrowHealthScore { get; set; }
    public decimal RegulatoryComplianceScore { get; set; }
    public decimal FinancialSoundnessScore { get; set; }
    public decimal HistoricalSuccessScore { get; set; }
}

public class ViolationDto
{
    public Guid Id { get; set; }
    public string Description { get; set; } = default!;
    public ViolationSeverity Severity { get; set; }
    public DateTime ViolationDate { get; set; }
    public bool IsResolved { get; set; }
}
