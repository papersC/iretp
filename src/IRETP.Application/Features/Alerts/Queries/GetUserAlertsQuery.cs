using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Alerts.Queries;

public class GetUserAlertsQuery : IRequest<List<InvestorAlertDto>>
{
    public string? UserId { get; set; }
}
