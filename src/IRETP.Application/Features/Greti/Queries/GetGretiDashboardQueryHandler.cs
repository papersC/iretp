using IRETP.Application.DTOs;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PriceIndexEntity = IRETP.Domain.Entities.PriceIndex;
using RentalIndexEntity = IRETP.Domain.Entities.RentalIndex;
using ZoneEntity = IRETP.Domain.Entities.Zone;
using TransactionEntity = IRETP.Domain.Entities.Transaction;
using ProjectEntity = IRETP.Domain.Entities.Project;
using DeveloperEntity = IRETP.Domain.Entities.Developer;
using DeveloperScoreEntity = IRETP.Domain.Entities.DeveloperScore;

namespace IRETP.Application.Features.Greti.Queries;

/// <summary>
/// Computes the GRETI dashboard from live DLD data. The six JLL sub-indices
/// (RFP Section 2.2) share the same baselines/targets used on the marketing
/// page, but the Delivered % is derived from what's actually present in the
/// database so DLD leadership can see platform progress in real numbers.
/// Lower score = more transparent. Formula per sub-index:
///   current = baseline − (baseline − target) × (deliveredPct / 100)
/// so full delivery lands exactly on the target.
/// </summary>
public class GetGretiDashboardQueryHandler : IRequestHandler<GetGretiDashboardQuery, GretiDashboardDto>
{
    private readonly IRepository<ZoneEntity> _zoneRepo;
    private readonly IRepository<PriceIndexEntity> _priceRepo;
    private readonly IRepository<RentalIndexEntity> _rentalRepo;
    private readonly IRepository<TransactionEntity> _transactionRepo;
    private readonly IRepository<ProjectEntity> _projectRepo;
    private readonly IRepository<DeveloperEntity> _developerRepo;
    private readonly IRepository<DeveloperScoreEntity> _scoreRepo;

    public GetGretiDashboardQueryHandler(
        IRepository<ZoneEntity> zoneRepo,
        IRepository<PriceIndexEntity> priceRepo,
        IRepository<RentalIndexEntity> rentalRepo,
        IRepository<TransactionEntity> transactionRepo,
        IRepository<ProjectEntity> projectRepo,
        IRepository<DeveloperEntity> developerRepo,
        IRepository<DeveloperScoreEntity> scoreRepo)
    {
        _zoneRepo = zoneRepo;
        _priceRepo = priceRepo;
        _rentalRepo = rentalRepo;
        _transactionRepo = transactionRepo;
        _projectRepo = projectRepo;
        _developerRepo = developerRepo;
        _scoreRepo = scoreRepo;
    }

    public async Task<GretiDashboardDto> Handle(GetGretiDashboardQuery request, CancellationToken ct)
    {
        // Raw counts
        var zoneCount = await _zoneRepo.Query().CountAsync(ct);
        var currentYear = DateTime.UtcNow.Year;
        var currentQuarter = ((DateTime.UtcNow.Month - 1) / 3) + 1;

        var zonesWithPrice = await _priceRepo.Query()
            .Where(p => p.Year == currentYear)
            .Select(p => p.ZoneId)
            .Distinct()
            .CountAsync(ct);

        var zonesWithRental = await _rentalRepo.Query()
            .Where(r => r.Year == currentYear)
            .Select(r => r.ZoneId)
            .Distinct()
            .CountAsync(ct);

        var earliestTx = await _transactionRepo.Query()
            .OrderBy(t => t.TransactionDate)
            .Select(t => (DateTime?)t.TransactionDate)
            .FirstOrDefaultAsync(ct);

        var txYearsAvailable = earliestTx.HasValue
            ? Math.Max(0, (DateTime.UtcNow.Year - earliestTx.Value.Year))
            : 0;

        var developerCount = await _developerRepo.Query().CountAsync(ct);
        var developersScored = await _scoreRepo.Query()
            .Where(s => s.Year == currentYear)
            .Select(s => s.DeveloperId)
            .Distinct()
            .CountAsync(ct);

        var projectCount = await _projectRepo.Query().CountAsync(ct);
        var projectsWithEsg = 0; // ESG/Estidama data layer is Phase 4; placeholder until data schema lands

        // Per-sub-index "Delivered %" derived from data coverage
        var investmentDelivered = PercentOf(zonesWithPrice + zonesWithRental, zoneCount * 2);
        var marketFundamentalsDelivered = ClampPct(
            (txYearsAvailable >= 5 ? 60 : txYearsAvailable * 12)    // 5y registry = 60 pts
            + (zonesWithPrice > 0 ? 20 : 0)                          // price index live = 20 pts
            + (zonesWithRental > 0 ? 20 : 0));                       // rental index live = 20 pts
        var transactionProcessDelivered = txYearsAvailable >= 5 ? 100
            : txYearsAvailable >= 3 ? 75
            : txYearsAvailable >= 1 ? 50
            : 25;
        var governanceDelivered = PercentOf(developersScored, developerCount);
        var technologyDelivered = 100; // AI agent + analytics engine live in Phase 1
        var sustainabilityDelivered = PercentOf(projectsWithEsg, projectCount);

        var subIndices = new List<GretiSubIndexDto>
        {
            BuildSubIndex("Investment Performance Measurement", 20, 3.15m, 1.85m,
                "Real-time price/sqft index, rental-yield heatmaps, 10-year transaction history.",
                "Phase 1", investmentDelivered),
            BuildSubIndex("Market Fundamentals & Data Availability", 20, 3.02m, 1.75m,
                "Open Data API, Excel/CSV/PDF/JSON exports, zone + period filters.",
                "Phase 1", marketFundamentalsDelivered),
            BuildSubIndex("Transaction Process Transparency", 15, 2.87m, 1.65m,
                "Searchable full transaction registry (sales / gifts / mortgages / auctions).",
                "Phase 1", transactionProcessDelivered),
            BuildSubIndex("Governance of Listed Vehicles", 15, 3.45m, 1.95m,
                "Public Developer Scorecard + Internal Developer Rating.",
                "Phase 3", governanceDelivered),
            BuildSubIndex("Technology & AI Integration", 15, 3.30m, 1.45m,
                "RAG-grounded AI Agent, Slice & Dice analytics, in-chat dashboards.",
                "Phase 1 onward", technologyDelivered),
            BuildSubIndex("Sustainability / ESG", 15, 3.62m, 2.10m,
                "LEED/Estidama Pearl data layer on GIS map + International benchmarking.",
                "Phase 4", sustainabilityDelivered)
        };

        // Weighted composite
        var totalWeight = subIndices.Sum(s => s.Weight);
        var compositeCurrent = totalWeight == 0
            ? 0m
            : Math.Round(subIndices.Sum(s => s.Current2024 * s.Weight) / totalWeight, 2);
        var compositeBaseline = Math.Round(subIndices.Sum(s => s.Baseline2022 * s.Weight) / totalWeight, 2);
        var compositeTarget = Math.Round(subIndices.Sum(s => s.Target * s.Weight) / totalWeight, 2);
        var projectedLift = Math.Round(compositeBaseline - compositeTarget, 2);

        // Trajectory
        var trajectory = new List<GretiTrajectoryPoint>
        {
            new() { Label = "2018", Composite = 3.15m, TierThreshold = 1.9m },
            new() { Label = "2020", Composite = 2.92m, TierThreshold = 1.9m },
            new() { Label = "2022", Composite = compositeBaseline, TierThreshold = 1.9m },
            new() { Label = "2024", Composite = compositeCurrent, TierThreshold = 1.9m },
            new()
            {
                Label = "2025 (proj)",
                Composite = Math.Round(compositeCurrent - projectedLift * 0.35m, 2),
                TierThreshold = 1.9m, IsProjection = true
            },
            new()
            {
                Label = "2026 (proj)",
                Composite = Math.Round(compositeCurrent - projectedLift * 0.70m, 2),
                TierThreshold = 1.9m, IsProjection = true
            },
            new()
            {
                Label = "2027 target",
                Composite = compositeTarget,
                TierThreshold = 1.9m, IsProjection = true
            }
        };

        var tier = compositeCurrent < 1.90m ? "Highly Transparent"
            : compositeCurrent < 2.45m ? "Transparent"
            : compositeCurrent < 3.00m ? "Semi-Transparent"
            : compositeCurrent < 3.45m ? "Low Transparency"
            : "Opaque";

        return new GretiDashboardDto
        {
            CompositeScore = compositeCurrent,
            CompositeBaseline2022 = compositeBaseline,
            CompositeTarget = compositeTarget,
            ProjectedLift = projectedLift,
            GlobalRank2024 = 28, // JLL 2024 published ranking for Dubai
            Tier = tier,
            SubIndices = subIndices,
            Trajectory = trajectory
        };
    }

    private static GretiSubIndexDto BuildSubIndex(
        string name, int weight, decimal baseline, decimal target,
        string lever, string phase, int deliveredPct)
    {
        deliveredPct = Math.Clamp(deliveredPct, 0, 100);
        var current = Math.Round(baseline - (baseline - target) * deliveredPct / 100m, 2);
        return new GretiSubIndexDto
        {
            Name = name,
            Weight = weight,
            Baseline2022 = baseline,
            Current2024 = current,
            Target = target,
            DeliveredPct = deliveredPct,
            Phase = phase,
            IretpLever = lever
        };
    }

    private static int PercentOf(int numerator, int denominator) =>
        denominator == 0 ? 0 : ClampPct((numerator * 100) / denominator);

    private static int ClampPct(int value) => Math.Clamp(value, 0, 100);
}
