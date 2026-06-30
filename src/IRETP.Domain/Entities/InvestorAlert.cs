using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class InvestorAlert : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string AlertType { get; set; } = default!; // PriceMovement, NewProject, WatchlistChange, RentalYield, MarketDigest, RegulationUpdate
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public Guid? DeveloperId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal? ThresholdValue { get; set; }
    public string? ThresholdDirection { get; set; } // Above, Below
    public string? Frequency { get; set; } // Weekly, Monthly
    public bool IsEmailEnabled { get; set; } = true;
    public bool IsSmsEnabled { get; set; }
    public bool IsPushEnabled { get; set; }
    public bool IsActive { get; set; } = true;
}
