namespace IRETP.Application.DTOs;

public class DeveloperComparisonDto
{
    public List<DeveloperComparisonItem> Developers { get; set; } = [];
}

public class DeveloperComparisonItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public decimal CompositeScore { get; set; }
    public decimal OnTimeDeliveryScore { get; set; }
    public decimal UnitSalesCompletionScore { get; set; }
    public decimal EscrowHealthScore { get; set; }
    public decimal RegulatoryComplianceScore { get; set; }
    public decimal FinancialSoundnessScore { get; set; }
    public decimal HistoricalSuccessScore { get; set; }
    public int TotalProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int TotalUnitsDelivered { get; set; }
    public string RiskBadge { get; set; } = default!;
}
