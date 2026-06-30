using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class GetPublicDeveloperScorecardsQueryHandler
    : IRequestHandler<GetPublicDeveloperScorecardsQuery, List<PublicDeveloperScorecardDto>>
{
    private readonly IRepository<Developer> _developerRepo;
    private readonly IRepository<DeveloperScore> _scoreRepo;
    private readonly IRepository<Project> _projectRepo;

    public GetPublicDeveloperScorecardsQueryHandler(
        IRepository<Developer> developerRepo,
        IRepository<DeveloperScore> scoreRepo,
        IRepository<Project> projectRepo)
    {
        _developerRepo = developerRepo;
        _scoreRepo = scoreRepo;
        _projectRepo = projectRepo;
    }

    public Task<List<PublicDeveloperScorecardDto>> Handle(
        GetPublicDeveloperScorecardsQuery request, CancellationToken cancellationToken)
    {
        var activeDevelopers = _developerRepo.Query()
            .Where(d => d.IsActive)
            .ToList();

        var developerIds = activeDevelopers.Select(d => d.Id).ToList();

        // Get latest scores per developer
        var latestScores = _scoreRepo.Query()
            .Where(s => developerIds.Contains(s.DeveloperId))
            .ToList()
            .GroupBy(s => s.DeveloperId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.Year).ThenByDescending(s => s.Quarter).First());

        // Get project stats per developer
        var projectStats = _projectRepo.Query()
            .Where(p => developerIds.Contains(p.DeveloperId))
            .ToList()
            .GroupBy(p => p.DeveloperId)
            .ToDictionary(g => g.Key, g => new
            {
                CompletedProjects = g.Count(p => p.Status == ProjectStatus.Completed),
                TotalUnitsDelivered = g.Where(p => p.Status == ProjectStatus.Completed).Sum(p => p.TotalUnits)
            });

        var result = activeDevelopers.Select(d =>
        {
            latestScores.TryGetValue(d.Id, out var score);
            projectStats.TryGetValue(d.Id, out var stats);

            return new PublicDeveloperScorecardDto
            {
                Id = d.Id,
                Name = d.Name,
                NameAr = d.NameAr,
                CompletedProjects = stats?.CompletedProjects ?? 0,
                OnTimeDeliveryPercentage = score?.OnTimeDeliveryScore ?? 0,
                ReraComplianceRating = GetComplianceRating(score?.RegulatoryComplianceScore ?? 0),
                TotalUnitsDelivered = stats?.TotalUnitsDelivered ?? 0
            };
        }).ToList();

        return Task.FromResult(result);
    }

    private static string GetComplianceRating(decimal complianceScore) => complianceScore switch
    {
        >= 90 => "Excellent",
        >= 70 => "Good",
        >= 50 => "Fair",
        _ => "Poor"
    };
}
