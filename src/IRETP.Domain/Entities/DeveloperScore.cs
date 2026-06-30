using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class DeveloperScore : BaseEntity
{
    public Guid DeveloperId { get; set; }
    public Developer Developer { get; set; } = default!;
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal OnTimeDeliveryScore { get; set; }
    public decimal UnitSalesCompletionScore { get; set; }
    public decimal EscrowHealthScore { get; set; }
    public decimal RegulatoryComplianceScore { get; set; }
    public decimal FinancialSoundnessScore { get; set; }
    public decimal HistoricalSuccessScore { get; set; }
    public decimal CompositeScore { get; set; }

    // Weights applied at time of calculation
    public decimal OnTimeDeliveryWeight { get; set; }
    public decimal UnitSalesWeight { get; set; }
    public decimal EscrowHealthWeight { get; set; }
    public decimal RegulatoryComplianceWeight { get; set; }
    public decimal FinancialSoundnessWeight { get; set; }
    public decimal HistoricalSuccessWeight { get; set; }
}
