namespace IRETP.Application.DTOs;

public class DeveloperScoreDto
{
    public Guid DeveloperId { get; set; }
    public string DeveloperName { get; set; } = default!;
    public string DeveloperNameAr { get; set; } = default!;
    public string LicenceNumber { get; set; } = default!;
    public bool IsActive { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal OnTimeDeliveryScore { get; set; }
    public decimal UnitSalesCompletionScore { get; set; }
    public decimal EscrowHealthScore { get; set; }
    public decimal RegulatoryComplianceScore { get; set; }
    public decimal FinancialSoundnessScore { get; set; }
    public decimal HistoricalSuccessScore { get; set; }
    public decimal CompositeScore { get; set; }
    public string RiskBadge { get; set; } = default!;
    public decimal? TrendVsPreviousQuarter { get; set; }
}
