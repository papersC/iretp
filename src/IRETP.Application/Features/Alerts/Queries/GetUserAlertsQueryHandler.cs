using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Queries;

public class GetUserAlertsQueryHandler
    : IRequestHandler<GetUserAlertsQuery, List<InvestorAlertDto>>
{
    private readonly IRepository<InvestorAlert> _alertRepo;

    public GetUserAlertsQueryHandler(IRepository<InvestorAlert> alertRepo)
    {
        _alertRepo = alertRepo;
    }

    public async Task<List<InvestorAlertDto>> Handle(
        GetUserAlertsQuery request, CancellationToken cancellationToken)
    {
        var alerts = await _alertRepo.FindAsync(
            a => a.UserId == request.UserId, cancellationToken);

        return alerts.Select(a => new InvestorAlertDto
        {
            Id = a.Id,
            AlertType = a.AlertType,
            ZoneId = a.ZoneId,
            ZoneName = a.Zone?.Name,
            DeveloperId = a.DeveloperId,
            ProjectId = a.ProjectId,
            ThresholdValue = a.ThresholdValue,
            ThresholdDirection = a.ThresholdDirection,
            Frequency = a.Frequency,
            IsEmailEnabled = a.IsEmailEnabled,
            IsSmsEnabled = a.IsSmsEnabled,
            IsPushEnabled = a.IsPushEnabled,
            IsActive = a.IsActive
        }).ToList();
    }
}
