using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Esg.Queries;

/// <summary>
/// Aggregates certification coverage across the project registry and returns a
/// single payload for the public ESG page and the GIS heatmap (RFP 20).
/// Coverage and average-level are the signals the GRETI Sustainability sub-
/// index responds to, so both are exposed per zone.
/// </summary>
public class GetEsgDashboardQueryHandler : IRequestHandler<GetEsgDashboardQuery, EsgDashboardDto>
{
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<ProjectCertification> _certRepo;

    public GetEsgDashboardQueryHandler(
        IRepository<Project> projectRepo,
        IRepository<ProjectCertification> certRepo)
    {
        _projectRepo = projectRepo;
        _certRepo = certRepo;
    }

    public async Task<EsgDashboardDto> Handle(GetEsgDashboardQuery request, CancellationToken ct)
    {
        var projects = await _projectRepo.Query()
            .Include(p => p.Zone)
            .Include(p => p.Developer)
            .ToListAsync(ct);

        var certifications = await _certRepo.Query()
            .Include(c => c.Project).ThenInclude(p => p.Zone)
            .Include(c => c.Project).ThenInclude(p => p.Developer)
            .ToListAsync(ct);

        // Pick the best (highest level) active certification per project so the
        // "certified coverage" metric counts each project at most once.
        var now = DateTime.UtcNow;
        var bestByProject = certifications
            .Where(c => !c.ExpiresAt.HasValue || c.ExpiresAt.Value > now)
            .GroupBy(c => c.ProjectId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => (int)c.Level)
                      .ThenByDescending(c => c.AwardedAt)
                      .First());

        var certifiedCount = bestByProject.Count;
        var totalProjects = projects.Count;
        var coverage = totalProjects == 0
            ? 0m
            : Math.Round((decimal)certifiedCount / totalProjects * 100m, 1);
        var totalUnitsCertified = projects
            .Where(p => bestByProject.ContainsKey(p.Id))
            .Sum(p => p.TotalUnits);

        var bySchemes = certifications
            .GroupBy(c => c.Scheme.ToString())
            .Select(g => new EsgSchemeSummary
            {
                Scheme = g.Key,
                Count = g.Count(),
                AverageScorePct = Math.Round(
                    g.Where(c => c.ScorePct.HasValue).Select(c => c.ScorePct!.Value).DefaultIfEmpty(0m).Average(), 1)
            })
            .OrderByDescending(s => s.Count)
            .ToList();

        var byLevels = certifications
            .GroupBy(c => c.Level.ToString())
            .Select(g => new EsgLevelSummary { Level = g.Key, Count = g.Count() })
            .OrderBy(l => l.Level)
            .ToList();

        var byZones = projects
            .GroupBy(p => new { p.ZoneId, ZoneName = p.Zone?.Name ?? "—" })
            .Select(g =>
            {
                var certifiedInZone = g.Count(p => bestByProject.ContainsKey(p.Id));
                var avgLevel = g
                    .Where(p => bestByProject.ContainsKey(p.Id))
                    .Select(p => (decimal)(int)bestByProject[p.Id].Level)
                    .DefaultIfEmpty(0m)
                    .Average();
                return new EsgZoneItem
                {
                    ZoneId = g.Key.ZoneId,
                    ZoneName = g.Key.ZoneName,
                    ProjectCount = g.Count(),
                    CertifiedProjectCount = certifiedInZone,
                    CoveragePct = g.Count() == 0
                        ? 0m
                        : Math.Round((decimal)certifiedInZone / g.Count() * 100m, 1),
                    AverageLevel = Math.Round(avgLevel, 2)
                };
            })
            .OrderByDescending(z => z.CoveragePct)
            .ToList();

        var topProjects = bestByProject.Values
            .OrderByDescending(c => (int)c.Level)
            .ThenByDescending(c => c.AwardedAt)
            .Take(25)
            .Select(c => new EsgCertifiedProjectDto
            {
                ProjectId = c.ProjectId,
                ProjectName = c.Project.Name,
                ProjectNameAr = c.Project.NameAr,
                DeveloperName = c.Project.Developer?.Name ?? "—",
                ZoneName = c.Project.Zone?.Name ?? "—",
                Latitude = c.Project.Latitude,
                Longitude = c.Project.Longitude,
                Scheme = c.Scheme.ToString(),
                Level = c.Level.ToString(),
                ScorePct = c.ScorePct,
                AwardedAt = c.AwardedAt,
                CertificateNumber = c.CertificateNumber
            })
            .ToList();

        return new EsgDashboardDto
        {
            TotalCertifiedProjects = certifiedCount,
            TotalProjects = totalProjects,
            CertifiedCoveragePct = coverage,
            TotalUnitsInCertifiedProjects = totalUnitsCertified,
            BySchemes = bySchemes,
            ByLevels = byLevels,
            ByZones = byZones,
            TopProjects = topProjects
        };
    }
}
