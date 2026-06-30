using IRETP.Domain.Common;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Repositories;
using IRETP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IRETP.Tests.Infrastructure;

/// <summary>
/// Covers all ten RFP §8.1 risk indicators — especially the indicator-key
/// alignment fix and the five indicators that previously had no detection
/// logic (transaction volume decline, developer score deterioration, high-risk
/// concentration, price decline, severe regulatory violation).
/// </summary>
public class RiskEngineServiceTests
{
    private readonly string _dbName = $"risk-engine-{Guid.NewGuid():N}";

    // Minimal default threshold catalog matching the DbSeeder §8.1 defaults.
    private static List<RiskThreshold> SeedThresholds() => new()
    {
        Threshold(RiskEngineService.ProjectDelayWarningKey,       6,  RiskLevel.Warning, AlertLevel.Level1_Operational),
        Threshold(RiskEngineService.ProjectDelayCriticalKey,      12, RiskLevel.High,    AlertLevel.Level3_SeniorLeadership),
        Threshold(RiskEngineService.EscrowShortfallWarningKey,    80, RiskLevel.Warning, AlertLevel.Level2_Managerial),
        Threshold(RiskEngineService.EscrowShortfallCriticalKey,   60, RiskLevel.High,    AlertLevel.Level4_Strategic),
        Threshold(RiskEngineService.ConstructionSuspensionKey,    30, RiskLevel.Warning, AlertLevel.Level2_Managerial),
        Threshold(RiskEngineService.TransactionVolumeDeclineKey,  40, RiskLevel.Medium,  AlertLevel.Level2_Managerial),
        Threshold(RiskEngineService.DeveloperScoreDeteriorationKey,15,RiskLevel.Medium,  AlertLevel.Level2_Managerial),
        Threshold(RiskEngineService.HighRiskConcentrationKey,     30, RiskLevel.High,    AlertLevel.Level3_SeniorLeadership),
        Threshold(RiskEngineService.PriceDeclineKey,              15, RiskLevel.Medium,  AlertLevel.Level2_Managerial),
        Threshold(RiskEngineService.SevereRegulatoryViolationKey, 1,  RiskLevel.High,    AlertLevel.Level3_SeniorLeadership)
    };

    private static RiskThreshold Threshold(string key, decimal value, RiskLevel level, AlertLevel alertLevel) =>
        new() { IndicatorKey = key, IndicatorName = key, IndicatorNameAr = key, ThresholdValue = value,
                DefaultRiskLevel = level, DefaultAlertLevel = alertLevel, ThresholdUnit = "x" };

    private (IServiceProvider sp, IretpDbContext seedDb) BuildServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<IretpDbContext>(o => o.UseInMemoryDatabase(_dbName), ServiceLifetime.Scoped);
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<RiskEngineService>(p =>
            new RiskEngineService(p.GetRequiredService<IServiceScopeFactory>(), NullLogger<RiskEngineService>.Instance));
        var sp = services.BuildServiceProvider();
        var seedDb = sp.CreateScope().ServiceProvider.GetRequiredService<IretpDbContext>();
        return (sp, seedDb);
    }

    private static async Task<List<RiskAlert>> GetAlertsAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IretpDbContext>()
            .RiskAlerts.ToListAsync();
    }

    // --- Project Delivery Delay (regression for the key-mismatch fix) -----

    [Fact]
    public async Task ProjectDelayCritical_fires_when_delay_and_completion_pass_thresholds()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (dev, zone) = SeedDeveloperAndZone(db);
        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(), Name = "Tower X", NameAr = "برج",
            DeveloperId = dev.Id, ZoneId = zone.Id,
            Status = ProjectStatus.UnderConstruction,
            ExpectedDeliveryDate = DateTime.UtcNow.AddMonths(-13),
            CompletionPercentage = 80
        });
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a => a.IndicatorType == RiskEngineService.ProjectDelayCriticalKey);
    }

    // --- Escrow Shortfall (regression for the key-mismatch fix) ----------

    [Fact]
    public async Task EscrowShortfallWarning_fires_when_adequacy_below_seeded_threshold()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (dev, zone) = SeedDeveloperAndZone(db);
        var project = new Project
        {
            Id = Guid.NewGuid(), Name = "Tower Y", NameAr = "برج",
            DeveloperId = dev.Id, ZoneId = zone.Id,
            Status = ProjectStatus.UnderConstruction,
            CompletionPercentage = 50,
            EscrowAccount = new EscrowAccount
            {
                Id = Guid.NewGuid(), AccountNumber = "ESC-001", BankName = "Emirates NBD",
                CurrentBalance = 70, RequiredMinimumBalance = 100,
                AdequacyRatio = 0.70m, Status = EscrowStatus.Warning
            }
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a => a.IndicatorType == RiskEngineService.EscrowShortfallWarningKey);
    }

    // --- Construction Activity Suspension (now threshold-driven) --------

    [Fact]
    public async Task ConstructionSuspension_fires_warning_for_newly_stalled_project()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (dev, zone) = SeedDeveloperAndZone(db);
        db.Projects.Add(new Project
        {
            Id = Guid.NewGuid(), Name = "Stalled Tower", NameAr = "برج",
            DeveloperId = dev.Id, ZoneId = zone.Id,
            Status = ProjectStatus.Stalled
        });
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        // With no prior suspension alert on record, the engine uses a 31-day
        // fallback — just over the 30-day warning threshold but under the
        // 60-day (2× threshold) critical cutoff.
        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a =>
            a.IndicatorType == RiskEngineService.ConstructionSuspensionKey
            && a.RiskLevel == RiskLevel.Warning
            && a.AlertLevel == AlertLevel.Level2_Managerial);
    }

    // --- §8.1 row 6 — Transaction Volume Decline ------------------------

    [Fact]
    public async Task TransactionVolumeDecline_fires_when_last_month_drops_past_threshold()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (_, zone) = SeedDeveloperAndZone(db);

        // 110 transactions over months 2..12 → ~10/month baseline; 2 in last 30 days → 80% drop.
        var now = DateTime.UtcNow;
        for (var month = 2; month <= 12; month++)
        for (var i = 0; i < 10; i++)
        {
            db.Transactions.Add(NewTransaction(zone.Id, now.AddDays(-30 * month - i)));
        }
        db.Transactions.Add(NewTransaction(zone.Id, now.AddDays(-10)));
        db.Transactions.Add(NewTransaction(zone.Id, now.AddDays(-20)));
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a =>
            a.IndicatorType == RiskEngineService.TransactionVolumeDeclineKey
            && a.ZoneId == zone.Id);
    }

    // --- §8.1 row 9 — Price Decline -------------------------------------

    [Fact]
    public async Task PriceDecline_fires_when_current_quarter_drops_past_threshold()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (_, zone) = SeedDeveloperAndZone(db);

        // Prior quarter (days 91–180) at AED 1,000/sqft; current quarter (days 1–90) at AED 700 → 30% drop.
        var now = DateTime.UtcNow;
        for (var i = 0; i < 10; i++)
        {
            db.Transactions.Add(NewTransaction(zone.Id, now.AddDays(-100 - i), pricePerSqft: 1000));
            db.Transactions.Add(NewTransaction(zone.Id, now.AddDays(-20 - i),  pricePerSqft: 700));
        }
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a =>
            a.IndicatorType == RiskEngineService.PriceDeclineKey
            && a.ZoneId == zone.Id);
    }

    // --- §8.1 row 7 — Developer Score Deterioration ---------------------

    [Fact]
    public async Task DeveloperScoreDeterioration_fires_when_composite_drops_past_threshold()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (dev, _) = SeedDeveloperAndZone(db);
        db.DeveloperScores.Add(new DeveloperScore { Id = Guid.NewGuid(), DeveloperId = dev.Id, Year = 2025, Quarter = 4, CompositeScore = 75 });
        db.DeveloperScores.Add(new DeveloperScore { Id = Guid.NewGuid(), DeveloperId = dev.Id, Year = 2026, Quarter = 1, CompositeScore = 55 });
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a =>
            a.IndicatorType == RiskEngineService.DeveloperScoreDeteriorationKey
            && a.DeveloperId == dev.Id
            && a.RiskLevel == RiskLevel.Medium);
    }

    [Fact]
    public async Task DeveloperScoreDeterioration_escalates_to_high_when_new_score_below_forty()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (dev, _) = SeedDeveloperAndZone(db);
        db.DeveloperScores.Add(new DeveloperScore { Id = Guid.NewGuid(), DeveloperId = dev.Id, Year = 2025, Quarter = 4, CompositeScore = 55 });
        db.DeveloperScores.Add(new DeveloperScore { Id = Guid.NewGuid(), DeveloperId = dev.Id, Year = 2026, Quarter = 1, CompositeScore = 35 });
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a =>
            a.IndicatorType == RiskEngineService.DeveloperScoreDeteriorationKey
            && a.RiskLevel == RiskLevel.High);
    }

    // --- §8.1 row 8 — High-Risk Concentration ---------------------------

    [Fact]
    public async Task HighRiskConcentration_fires_when_portfolio_share_exceeds_threshold()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (dev, zone) = SeedDeveloperAndZone(db);

        // 4 active projects, 2 stalled → 50% concentration, past 30% threshold.
        for (var i = 0; i < 4; i++)
        {
            db.Projects.Add(new Project
            {
                Id = Guid.NewGuid(), Name = $"P{i}", NameAr = "p",
                DeveloperId = dev.Id, ZoneId = zone.Id,
                Status = i < 2 ? ProjectStatus.Stalled : ProjectStatus.UnderConstruction,
                CompletionPercentage = 50
            });
        }
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a =>
            a.IndicatorType == RiskEngineService.HighRiskConcentrationKey
            && a.DeveloperId == dev.Id);
    }

    // --- §8.1 row 10 — Severe Regulatory Violation ----------------------

    [Fact]
    public async Task SevereRegulatoryViolation_fires_for_recent_critical_violation()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (dev, _) = SeedDeveloperAndZone(db);
        db.RegulatoryViolations.Add(new RegulatoryViolation
        {
            Id = Guid.NewGuid(), DeveloperId = dev.Id,
            Severity = ViolationSeverity.Critical,
            ViolationDate = DateTime.UtcNow.AddDays(-5),
            Description = "Undisclosed off-plan sales"
        });
        await db.SaveChangesAsync();

        await sp.GetRequiredService<RiskEngineService>().EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Contains(alerts, a =>
            a.IndicatorType == RiskEngineService.SevereRegulatoryViolationKey
            && a.DeveloperId == dev.Id);
    }

    // --- Dedup ----------------------------------------------------------

    [Fact]
    public async Task Rerun_does_not_duplicate_open_alerts()
    {
        var (sp, db) = BuildServices();
        db.RiskThresholds.AddRange(SeedThresholds());
        var (dev, _) = SeedDeveloperAndZone(db);
        db.RegulatoryViolations.Add(new RegulatoryViolation
        {
            Id = Guid.NewGuid(), DeveloperId = dev.Id,
            Severity = ViolationSeverity.Critical,
            ViolationDate = DateTime.UtcNow.AddDays(-3),
            Description = "Breach"
        });
        await db.SaveChangesAsync();

        var engine = sp.GetRequiredService<RiskEngineService>();
        await engine.EvaluateRiskIndicatorsAsync();
        await engine.EvaluateRiskIndicatorsAsync();

        var alerts = await GetAlertsAsync(sp);
        Assert.Single(alerts, a => a.IndicatorType == RiskEngineService.SevereRegulatoryViolationKey);
    }

    // --- Seed helpers ---------------------------------------------------

    private static (Developer dev, Zone zone) SeedDeveloperAndZone(IretpDbContext db)
    {
        var dev = new Developer { Id = Guid.NewGuid(), Name = "Dev Co", NameAr = "د", LicenceNumber = "L-01" };
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Zone A", NameAr = "منطقة" };
        db.Developers.Add(dev);
        db.Zones.Add(zone);
        db.SaveChanges();
        return (dev, zone);
    }

    private static Transaction NewTransaction(Guid zoneId, DateTime date, decimal pricePerSqft = 1000) => new()
    {
        Id = Guid.NewGuid(),
        ZoneId = zoneId,
        TransactionDate = date,
        AreaSqft = 1000,
        AreaSqm = 92,
        TransactionValue = 1_000_000,
        PricePerSqft = pricePerSqft,
        PropertyType = PropertyType.Apartment,
        TransactionType = TransactionType.Sale,
        FinancingMethod = FinancingMethod.Cash
    };
}
