using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Queries;

public class GetEwrsDashboardQueryHandler
    : IRequestHandler<GetEwrsDashboardQuery, EwrsDashboardDto>
{
    private readonly IRepository<RiskAlert> _alertRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<EscrowAccount> _escrowRepo;
    private readonly IRepository<Zone> _zoneRepo;

    public GetEwrsDashboardQueryHandler(
        IRepository<RiskAlert> alertRepo,
        IRepository<Project> projectRepo,
        IRepository<EscrowAccount> escrowRepo,
        IRepository<Zone> zoneRepo)
    {
        _alertRepo = alertRepo;
        _projectRepo = projectRepo;
        _escrowRepo = escrowRepo;
        _zoneRepo = zoneRepo;
    }

    public async Task<EwrsDashboardDto> Handle(
        GetEwrsDashboardQuery request, CancellationToken cancellationToken)
    {
        var alerts = _alertRepo.Query();
        var activeAlerts = alerts.Where(a => a.Status != AlertStatus.Resolved);

        // Count high-risk projects (projects with associated High or Critical risk alerts)
        var highRiskProjectIds = activeAlerts
            .Where(a => a.RiskLevel >= RiskLevel.High && a.ProjectId.HasValue)
            .Select(a => a.ProjectId!.Value)
            .Distinct();
        var totalHighRiskProjects = highRiskProjectIds.Count();

        // Count warning projects
        var warningProjectIds = activeAlerts
            .Where(a => a.RiskLevel == RiskLevel.Warning && a.ProjectId.HasValue)
            .Select(a => a.ProjectId!.Value)
            .Distinct();
        var totalWarningProjects = warningProjectIds.Count();

        // Count projects with escrow shortfall
        var projectsWithEscrowShortfall = await _escrowRepo.CountAsync(
            e => e.Status == EscrowStatus.Warning || e.Status == EscrowStatus.Critical,
            cancellationToken);

        // Count projects where construction is stalled
        var projectsWithConstructionHalt = await _projectRepo.CountAsync(
            p => p.Status == ProjectStatus.Stalled, cancellationToken);

        // Count total active alerts
        var totalActiveAlerts = activeAlerts.Count();

        // Count unacknowledged alerts (New status)
        var unacknowledgedAlerts = alerts
            .Count(a => a.Status == AlertStatus.New);

        // Group risk data by zone for heatmap
        var zones = await _zoneRepo.GetAllAsync(cancellationToken);
        var zoneRiskSummary = zones.Select(z =>
        {
            var zoneAlerts = activeAlerts.Where(a => a.ZoneId == z.Id);
            var zoneProjects = _projectRepo.Query().Where(p => p.ZoneId == z.Id);
            return new ZoneRiskSummaryDto
            {
                ZoneId = z.Id,
                ZoneName = z.Name,
                ZoneNameAr = z.NameAr,
                HighRiskCount = zoneAlerts.Count(a => a.RiskLevel >= RiskLevel.High),
                WarningCount = zoneAlerts.Count(a => a.RiskLevel == RiskLevel.Warning),
                TotalProjects = zoneProjects.Count(),
                GeoJson = z.GeoJson,
                CenterLat = z.CenterLat,
                CenterLng = z.CenterLng
            };
        }).Where(z => z.TotalProjects > 0).ToList();

        // Get 10 most recent alerts
        var recentAlerts = alerts
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
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
                CreatedAt = a.CreatedAt
            })
            .ToList();

        return new EwrsDashboardDto
        {
            TotalHighRiskProjects = totalHighRiskProjects,
            TotalWarningProjects = totalWarningProjects,
            ProjectsWithEscrowShortfall = projectsWithEscrowShortfall,
            ProjectsWithConstructionHalt = projectsWithConstructionHalt,
            TotalActiveAlerts = totalActiveAlerts,
            UnacknowledgedAlerts = unacknowledgedAlerts,
            ZoneRiskSummary = zoneRiskSummary,
            RecentAlerts = recentAlerts
        };
    }
}
