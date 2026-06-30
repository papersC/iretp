using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class CompareDevelopersQueryHandler
    : IRequestHandler<CompareDevelopersQuery, DeveloperComparisonDto>
{
    private readonly IRepository<Developer> _developerRepo;
    private readonly IRepository<DeveloperScore> _scoreRepo;
    private readonly IRepository<Project> _projectRepo;

    public CompareDevelopersQueryHandler(
        IRepository<Developer> developerRepo,
        IRepository<DeveloperScore> scoreRepo,
        IRepository<Project> projectRepo)
    {
        _developerRepo = developerRepo;
        _scoreRepo = scoreRepo;
        _projectRepo = projectRepo;
    }

    public async Task<DeveloperComparisonDto> Handle(
        CompareDevelopersQuery request, CancellationToken cancellationToken)
    {
        var ids = request.Ids.Take(4).ToList();

        var developers = _developerRepo.Query()
            .Where(d => ids.Contains(d.Id))
            .ToList();

        // Get the latest score for each developer
        var latestScores = _scoreRepo.Query()
            .Where(s => ids.Contains(s.DeveloperId))
            .ToList()
            .GroupBy(s => s.DeveloperId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.Year).ThenByDescending(s => s.Quarter).First());

        // Get project counts
        var projectData = _projectRepo.Query()
            .Where(p => ids.Contains(p.DeveloperId))
            .ToList()
            .GroupBy(p => p.DeveloperId)
            .ToDictionary(g => g.Key, g => new
            {
                TotalProjects = g.Count(),
                CompletedProjects = g.Count(p => p.Status == ProjectStatus.Completed),
                TotalUnitsDelivered = g.Where(p => p.Status == ProjectStatus.Completed).Sum(p => p.TotalUnits)
            });

        var items = developers.Select(d =>
        {
            latestScores.TryGetValue(d.Id, out var score);
            projectData.TryGetValue(d.Id, out var projects);

            return new DeveloperComparisonItem
            {
                Id = d.Id,
                Name = d.Name,
                NameAr = d.NameAr,
                CompositeScore = score?.CompositeScore ?? 0,
                OnTimeDeliveryScore = score?.OnTimeDeliveryScore ?? 0,
                UnitSalesCompletionScore = score?.UnitSalesCompletionScore ?? 0,
                EscrowHealthScore = score?.EscrowHealthScore ?? 0,
                RegulatoryComplianceScore = score?.RegulatoryComplianceScore ?? 0,
                FinancialSoundnessScore = score?.FinancialSoundnessScore ?? 0,
                HistoricalSuccessScore = score?.HistoricalSuccessScore ?? 0,
                TotalProjects = projects?.TotalProjects ?? 0,
                CompletedProjects = projects?.CompletedProjects ?? 0,
                TotalUnitsDelivered = projects?.TotalUnitsDelivered ?? 0,
                RiskBadge = GetRiskBadge(score?.CompositeScore ?? 0)
            };
        }).ToList();

        return await Task.FromResult(new DeveloperComparisonDto { Developers = items });
    }

    private static string GetRiskBadge(decimal compositeScore) => compositeScore switch
    {
        >= 80 => "Low Risk",
        >= 60 => "Moderate Risk",
        >= 40 => "Elevated Risk",
        _ => "High Risk"
    };
}
