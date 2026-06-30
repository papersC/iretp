using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class UpdateRiskThresholdCommand : IRequest<bool>
{
    public Guid ThresholdId { get; set; }
    public decimal ThresholdValue { get; set; }
    public RiskLevel DefaultRiskLevel { get; set; }
    public AlertLevel DefaultAlertLevel { get; set; }
    public string? UserId { get; set; }
}
