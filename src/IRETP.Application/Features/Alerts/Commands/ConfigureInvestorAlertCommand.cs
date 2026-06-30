using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class ConfigureInvestorAlertCommand : IRequest<Guid>
{
    public string? UserId { get; set; }
    public string AlertType { get; set; } = default!; // PriceMovement, NewProject, WatchlistChange, RentalYield, MarketDigest, RegulationUpdate
    public Guid? ZoneId { get; set; }
    public Guid? DeveloperId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal? ThresholdValue { get; set; }
    public string? ThresholdDirection { get; set; } // Above, Below
    public string? Frequency { get; set; } // Weekly, Monthly
    public bool IsEmailEnabled { get; set; } = true;
    public bool IsSmsEnabled { get; set; }
    public bool IsPushEnabled { get; set; }
}
