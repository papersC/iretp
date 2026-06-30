using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.EWRS.Queries;

public class GetRiskAlertsQuery : IRequest<PagedResult<RiskAlertDto>>
{
    public AlertStatus? Status { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
