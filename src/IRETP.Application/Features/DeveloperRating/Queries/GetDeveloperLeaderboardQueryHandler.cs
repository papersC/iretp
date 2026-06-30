using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class GetDeveloperLeaderboardQueryHandler
    : IRequestHandler<GetDeveloperLeaderboardQuery, List<DeveloperScoreDto>>
{
    private readonly IRepository<DeveloperScore> _scoreRepo;

    public GetDeveloperLeaderboardQueryHandler(IRepository<DeveloperScore> scoreRepo)
    {
        _scoreRepo = scoreRepo;
    }

    public async Task<List<DeveloperScoreDto>> Handle(
        GetDeveloperLeaderboardQuery request, CancellationToken cancellationToken)
    {
        var scores = _scoreRepo.Query().AsQueryable();

        // Determine which period to use
        int targetYear, targetQuarter;

        if (request.Year.HasValue && request.Quarter.HasValue)
        {
            targetYear = request.Year.Value;
            targetQuarter = request.Quarter.Value;
        }
        else
        {
            // Use the latest available quarter
            var latest = scores
                .OrderByDescending(s => s.Year).ThenByDescending(s => s.Quarter)
                .FirstOrDefault();

            if (latest == null)
                return await Task.FromResult(new List<DeveloperScoreDto>());

            targetYear = latest.Year;
            targetQuarter = latest.Quarter;
        }

        var currentScores = scores
            .Where(s => s.Year == targetYear && s.Quarter == targetQuarter)
            .OrderByDescending(s => s.CompositeScore)
            .ToList();

        // Get previous quarter for trend
        var prevQuarter = targetQuarter > 1 ? targetQuarter - 1 : 4;
        var prevYear = targetQuarter > 1 ? targetYear : targetYear - 1;

        var previousScores = scores
            .Where(s => s.Year == prevYear && s.Quarter == prevQuarter)
            .ToDictionary(s => s.DeveloperId, s => s.CompositeScore);

        var result = currentScores.Select(s =>
        {
            previousScores.TryGetValue(s.DeveloperId, out var prevScore);

            return new DeveloperScoreDto
            {
                DeveloperId = s.DeveloperId,
                DeveloperName = s.Developer.Name,
                DeveloperNameAr = s.Developer.NameAr,
                LicenceNumber = s.Developer.LicenceNumber,
                IsActive = s.Developer.IsActive,
                Year = s.Year,
                Quarter = s.Quarter,
                OnTimeDeliveryScore = s.OnTimeDeliveryScore,
                UnitSalesCompletionScore = s.UnitSalesCompletionScore,
                EscrowHealthScore = s.EscrowHealthScore,
                RegulatoryComplianceScore = s.RegulatoryComplianceScore,
                FinancialSoundnessScore = s.FinancialSoundnessScore,
                HistoricalSuccessScore = s.HistoricalSuccessScore,
                CompositeScore = s.CompositeScore,
                RiskBadge = GetRiskBadge(s.CompositeScore),
                TrendVsPreviousQuarter = prevScore > 0
                    ? s.CompositeScore - prevScore
                    : null
            };
        }).ToList();

        if (request.Top.HasValue)
            result = result.Take(request.Top.Value).ToList();

        return await Task.FromResult(result);
    }

    private static string GetRiskBadge(decimal compositeScore) => compositeScore switch
    {
        >= 80 => "Low Risk",
        >= 60 => "Moderate Risk",
        >= 40 => "Elevated Risk",
        _ => "High Risk"
    };
}
