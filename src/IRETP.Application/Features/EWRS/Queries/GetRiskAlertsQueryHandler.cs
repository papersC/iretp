using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Queries;

public class GetRiskAlertsQueryHandler
    : IRequestHandler<GetRiskAlertsQuery, PagedResult<RiskAlertDto>>
{
    private readonly IRepository<RiskAlert> _riskAlertRepo;

    public GetRiskAlertsQueryHandler(IRepository<RiskAlert> riskAlertRepo)
    {
        _riskAlertRepo = riskAlertRepo;
    }

    public async Task<PagedResult<RiskAlertDto>> Handle(
        GetRiskAlertsQuery request, CancellationToken cancellationToken)
    {
        var query = _riskAlertRepo.Query().AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(a => a.Status == request.Status.Value);

        if (request.RiskLevel.HasValue)
            query = query.Where(a => a.RiskLevel == request.RiskLevel.Value);

        if (request.DateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= request.DateTo.Value);

        query = query.OrderByDescending(a => a.CreatedAt);

        var totalCount = query.Count();

        var items = query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new RiskAlertDto
            {
                Id = a.Id,
                IndicatorType = a.IndicatorType,
                RiskLevel = a.RiskLevel,
                AlertLevel = a.AlertLevel,
                Status = a.Status,
                ProjectId = a.ProjectId,
                ProjectName = a.Project != null ? a.Project.Name : null,
                DeveloperId = a.DeveloperId,
                DeveloperName = a.Developer != null ? a.Developer.Name : null,
                ZoneId = a.ZoneId,
                ZoneName = a.Zone != null ? a.Zone.Name : null,
                Title = a.Title,
                Description = a.Description,
                AssignedTo = a.AssignedTo,
                AcknowledgedAt = a.AcknowledgedAt,
                AcknowledgedBy = a.AcknowledgedBy,
                ResolvedAt = a.ResolvedAt,
                ResolvedBy = a.ResolvedBy,
                ActionNotes = a.ActionNotes,
                EscalationPath = a.EscalationPath,
                PlaybookProgressJson = a.PlaybookProgressJson,
                CreatedAt = a.CreatedAt,
                AcknowledgeDeadline = a.AcknowledgeDeadline,
                ResolutionDeadline = a.ResolutionDeadline,
                LastEscalatedAt = a.LastEscalatedAt,
                AutoEscalated = a.AutoEscalated
            })
            .ToList();

        return await Task.FromResult(
            new PagedResult<RiskAlertDto>(items, totalCount, request.Page, request.PageSize));
    }
}
