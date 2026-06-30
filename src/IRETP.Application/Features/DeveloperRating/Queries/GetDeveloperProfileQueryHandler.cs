using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class GetDeveloperProfileQueryHandler
    : IRequestHandler<GetDeveloperProfileQuery, DeveloperProfileDto?>
{
    private readonly IRepository<Developer> _developerRepo;
    private readonly IRepository<DeveloperScore> _scoreRepo;
    private readonly IRepository<RegulatoryViolation> _violationRepo;
    private readonly IRepository<Project> _projectRepo;

    public GetDeveloperProfileQueryHandler(
        IRepository<Developer> developerRepo,
        IRepository<DeveloperScore> scoreRepo,
        IRepository<RegulatoryViolation> violationRepo,
        IRepository<Project> projectRepo)
    {
        _developerRepo = developerRepo;
        _scoreRepo = scoreRepo;
        _violationRepo = violationRepo;
        _projectRepo = projectRepo;
    }

    public async Task<DeveloperProfileDto?> Handle(
        GetDeveloperProfileQuery request, CancellationToken cancellationToken)
    {
        var developer = await _developerRepo.GetByIdAsync(request.DeveloperId, cancellationToken);
        if (developer == null)
            return null;

        var projects = _projectRepo.Query()
            .Where(p => p.DeveloperId == request.DeveloperId)
            .Select(p => new DeveloperProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                ZoneName = p.Zone.Name,
                Status = p.Status,
                CompletionPercentage = p.CompletionPercentage,
                TotalUnits = p.TotalUnits,
                EscrowBalance = p.EscrowAccount != null ? p.EscrowAccount.CurrentBalance : null,
                EscrowStatus = p.EscrowAccount != null ? p.EscrowAccount.Status : null
            })
            .ToList();

        var scores = _scoreRepo.Query()
            .Where(s => s.DeveloperId == request.DeveloperId)
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Quarter)
            .Select(s => new DeveloperScoreHistoryDto
            {
                Year = s.Year,
                Quarter = s.Quarter,
                CompositeScore = s.CompositeScore,
                OnTimeDeliveryScore = s.OnTimeDeliveryScore,
                UnitSalesCompletionScore = s.UnitSalesCompletionScore,
                EscrowHealthScore = s.EscrowHealthScore,
                RegulatoryComplianceScore = s.RegulatoryComplianceScore,
                FinancialSoundnessScore = s.FinancialSoundnessScore,
                HistoricalSuccessScore = s.HistoricalSuccessScore
            })
            .ToList();

        var violations = _violationRepo.Query()
            .Where(v => v.DeveloperId == request.DeveloperId)
            .OrderByDescending(v => v.ViolationDate)
            .Select(v => new ViolationDto
            {
                Id = v.Id,
                Description = v.Description,
                Severity = v.Severity,
                ViolationDate = v.ViolationDate,
                IsResolved = false // RegulatoryViolation does not have IsResolved; default to false
            })
            .ToList();

        var completedProjects = projects.Count(p => p.Status == ProjectStatus.Completed);
        var totalUnitsDelivered = projects
            .Where(p => p.Status == ProjectStatus.Completed)
            .Sum(p => p.TotalUnits);

        return new DeveloperProfileDto
        {
            Id = developer.Id,
            Name = developer.Name,
            NameAr = developer.NameAr,
            LicenceNumber = developer.LicenceNumber,
            ContactEmail = developer.ContactEmail,
            Website = developer.Website,
            IsActive = developer.IsActive,
            Projects = projects,
            ScoreHistory = scores,
            Violations = violations,
            TotalProjectsCompleted = completedProjects,
            TotalUnitsDelivered = totalUnitsDelivered,
            LatestCompositeScore = scores.FirstOrDefault()?.CompositeScore
        };
    }
}
