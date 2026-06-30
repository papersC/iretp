using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services;

public class DeveloperScoringService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeveloperScoringService> _logger;

    public DeveloperScoringService(IServiceScopeFactory scopeFactory, ILogger<DeveloperScoringService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task CalculateQuarterlyScoresAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var developerRepo = scope.ServiceProvider.GetRequiredService<IRepository<Developer>>();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IRepository<Project>>();
        var escrowRepo = scope.ServiceProvider.GetRequiredService<IRepository<EscrowAccount>>();
        var violationRepo = scope.ServiceProvider.GetRequiredService<IRepository<RegulatoryViolation>>();
        var weightRepo = scope.ServiceProvider.GetRequiredService<IRepository<ScoringWeight>>();
        var scoreRepo = scope.ServiceProvider.GetRequiredService<IRepository<DeveloperScore>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var year = now.Year;
        var quarter = (now.Month - 1) / 3 + 1;

        var weights = await weightRepo.Query().ToListAsync();
        var developers = await developerRepo.Query()
            .Include(d => d.Projects).ThenInclude(p => p.EscrowAccount)
            .Include(d => d.Violations)
            .Where(d => d.IsActive)
            .ToListAsync();

        var weightDict = weights.ToDictionary(w => w.CriterionKey, w => w.Weight / 100m);

        var newScores = new List<DeveloperScore>();

        foreach (var dev in developers)
        {
            var completedProjects = dev.Projects.Where(p => p.Status == ProjectStatus.Completed).ToList();
            var allProjects = dev.Projects.ToList();

            // 1. On-Time Delivery Score (25% default)
            var onTimeCount = completedProjects.Count(p =>
                p.ActualDeliveryDate.HasValue && p.ExpectedDeliveryDate.HasValue
                && p.ActualDeliveryDate <= p.ExpectedDeliveryDate);
            var onTimeScore = completedProjects.Count > 0
                ? (decimal)onTimeCount / completedProjects.Count * 100 : 50m;

            // 2. Unit Sales Completion (20% default)
            var totalUnits = allProjects.Sum(p => p.TotalUnits);
            var soldUnits = allProjects.Sum(p => p.SoldUnits);
            var unitSalesScore = totalUnits > 0 ? (decimal)soldUnits / totalUnits * 100 : 50m;

            // 3. Escrow Health (20% default)
            var projectsWithEscrow = allProjects.Where(p => p.EscrowAccount != null).ToList();
            var escrowScore = projectsWithEscrow.Count > 0
                ? projectsWithEscrow.Average(p => Math.Min(p.EscrowAccount!.AdequacyRatio * 100, 100))
                : 50m;

            // 4. Regulatory Compliance (15% default) - starts at 100, deductions
            var fiveYearsAgo = DateTime.UtcNow.AddYears(-5);
            var recentViolations = dev.Violations.Where(v => v.CreatedAt >= fiveYearsAgo).ToList();
            var complianceScore = 100m;
            foreach (var v in recentViolations)
            {
                complianceScore -= v.Severity switch
                {
                    ViolationSeverity.Minor => 2,
                    ViolationSeverity.Major => 10,
                    ViolationSeverity.Critical => 25,
                    _ => 0
                };
            }
            complianceScore = Math.Max(complianceScore, 0);

            // 5. Financial Soundness (10% default) - placeholder
            var financialScore = 70m; // Would need external data source

            // 6. Historical Success Rate (10% default)
            var tenYearsAgo = DateTime.UtcNow.AddYears(-10);
            var historicalProjects = allProjects.Where(p => p.CreatedAt >= tenYearsAgo).ToList();
            var historicalSuccess = historicalProjects.Count > 0
                ? (decimal)completedProjects.Count(p => p.CreatedAt >= tenYearsAgo) / historicalProjects.Count * 100
                : 50m;

            // Calculate composite score
            var composite =
                onTimeScore * weightDict.GetValueOrDefault("OnTimeDelivery", 0.25m) +
                unitSalesScore * weightDict.GetValueOrDefault("UnitSalesCompletion", 0.20m) +
                escrowScore * weightDict.GetValueOrDefault("EscrowHealth", 0.20m) +
                complianceScore * weightDict.GetValueOrDefault("RegulatoryCompliance", 0.15m) +
                financialScore * weightDict.GetValueOrDefault("FinancialSoundness", 0.10m) +
                historicalSuccess * weightDict.GetValueOrDefault("HistoricalSuccess", 0.10m);

            newScores.Add(new DeveloperScore
            {
                DeveloperId = dev.Id,
                Year = year,
                Quarter = quarter,
                OnTimeDeliveryScore = onTimeScore,
                UnitSalesCompletionScore = unitSalesScore,
                EscrowHealthScore = escrowScore,
                RegulatoryComplianceScore = complianceScore,
                FinancialSoundnessScore = financialScore,
                HistoricalSuccessScore = historicalSuccess,
                CompositeScore = composite,
                OnTimeDeliveryWeight = weightDict.GetValueOrDefault("OnTimeDelivery", 0.25m) * 100,
                UnitSalesWeight = weightDict.GetValueOrDefault("UnitSalesCompletion", 0.20m) * 100,
                EscrowHealthWeight = weightDict.GetValueOrDefault("EscrowHealth", 0.20m) * 100,
                RegulatoryComplianceWeight = weightDict.GetValueOrDefault("RegulatoryCompliance", 0.15m) * 100,
                FinancialSoundnessWeight = weightDict.GetValueOrDefault("FinancialSoundness", 0.10m) * 100,
                HistoricalSuccessWeight = weightDict.GetValueOrDefault("HistoricalSuccess", 0.10m) * 100
            });
        }

        // Remove existing scores for this quarter and add new ones
        var existingScores = await scoreRepo.Query()
            .Where(s => s.Year == year && s.Quarter == quarter)
            .ToListAsync();

        foreach (var existing in existingScores)
            scoreRepo.Remove(existing);

        await scoreRepo.AddRangeAsync(newScores);
        await unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Calculated quarterly scores for {Count} developers (Q{Quarter} {Year})",
            newScores.Count, quarter, year);
    }
}
