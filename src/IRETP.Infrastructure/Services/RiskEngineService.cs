using IRETP.Domain.Common;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Evaluates all ten RFP §8.1 risk indicators and emits RiskAlerts for any
/// new breaches. Indicator keys match the seeded RiskThreshold catalog
/// (see <see cref="Data.Seed.DbSeeder"/>); each indicator respects the
/// admin-configurable ThresholdValue.
/// </summary>
public class RiskEngineService
{
    // Seeded IndicatorKey constants — keep in lockstep with DbSeeder.
    public const string ProjectDelayWarningKey       = "ProjectDeliveryDelay_Warning";
    public const string ProjectDelayCriticalKey      = "ProjectDeliveryDelay_Critical";
    public const string EscrowShortfallWarningKey    = "EscrowShortfall_Warning";
    public const string EscrowShortfallCriticalKey   = "EscrowShortfall_Critical";
    public const string ConstructionSuspensionKey    = "ConstructionSuspension";
    public const string TransactionVolumeDeclineKey  = "TransactionVolumeDecline";
    public const string DeveloperScoreDeteriorationKey = "DeveloperScoreDeterioration";
    public const string HighRiskConcentrationKey     = "HighRiskConcentration";
    public const string PriceDeclineKey              = "PriceDecline";
    public const string SevereRegulatoryViolationKey = "SevereRegulatoryViolation";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RiskEngineService> _logger;

    public RiskEngineService(IServiceScopeFactory scopeFactory, ILogger<RiskEngineService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EvaluateRiskIndicatorsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var thresholds = await sp.GetRequiredService<IRepository<RiskThreshold>>().Query().ToListAsync();
        var thresholdByKey = thresholds.ToDictionary(t => t.IndicatorKey, StringComparer.OrdinalIgnoreCase);

        var projects = await sp.GetRequiredService<IRepository<Project>>().Query()
            .Include(p => p.Developer)
            .Include(p => p.Zone)
            .Include(p => p.EscrowAccount)
            .ToListAsync();

        // Need the full history (incl. resolved) so ConstructionSuspension can
        // backdate its "stalled since" anchor from the first alert ever raised.
        // Dedup below only considers non-resolved alerts.
        var allAlerts = await sp.GetRequiredService<IRepository<RiskAlert>>().Query()
            .ToListAsync();
        var existingAlerts = allAlerts.Where(a => a.Status != AlertStatus.Resolved).ToList();

        var candidates = new List<RiskAlert>();

        candidates.AddRange(EvaluateProjectLevelIndicators(projects, thresholdByKey, allAlerts));
        candidates.AddRange(await EvaluateTransactionVolumeDeclineAsync(sp, thresholdByKey));
        candidates.AddRange(await EvaluatePriceDeclineAsync(sp, thresholdByKey));
        candidates.AddRange(await EvaluateDeveloperScoreDeteriorationAsync(sp, thresholdByKey));
        candidates.AddRange(await EvaluateHighRiskConcentrationAsync(sp, thresholdByKey, projects));
        candidates.AddRange(await EvaluateSevereRegulatoryViolationAsync(sp, thresholdByKey));

        var uniqueAlerts = candidates
            .Where(na => !existingAlerts.Any(ea =>
                ea.IndicatorType == na.IndicatorType
                && ea.ProjectId == na.ProjectId
                && ea.DeveloperId == na.DeveloperId
                && ea.ZoneId == na.ZoneId
                && ea.Status != AlertStatus.Resolved))
            .GroupBy(a => (a.IndicatorType, a.ProjectId, a.DeveloperId, a.ZoneId))
            .Select(g => g.First())
            .ToList();

        if (uniqueAlerts.Count > 0)
        {
            await sp.GetRequiredService<IRepository<RiskAlert>>().AddRangeAsync(uniqueAlerts);
            await sp.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
            _logger.LogInformation("Generated {Count} new risk alerts across {Indicators} indicators",
                uniqueAlerts.Count, uniqueAlerts.Select(a => a.IndicatorType).Distinct().Count());
        }
    }

    // ------------------------------------------------------------------
    // Per-project indicators: delivery delay, escrow shortfall, stalled
    // ------------------------------------------------------------------
    private static IEnumerable<RiskAlert> EvaluateProjectLevelIndicators(
        List<Project> projects,
        Dictionary<string, RiskThreshold> thresholdByKey,
        List<RiskAlert> allAlerts)
    {
        var delayWarning  = thresholdByKey.GetValueOrDefault(ProjectDelayWarningKey);
        var delayCritical = thresholdByKey.GetValueOrDefault(ProjectDelayCriticalKey);
        var escrowWarning  = thresholdByKey.GetValueOrDefault(EscrowShortfallWarningKey);
        var escrowCritical = thresholdByKey.GetValueOrDefault(EscrowShortfallCriticalKey);
        var suspension    = thresholdByKey.GetValueOrDefault(ConstructionSuspensionKey);

        foreach (var project in projects)
        {
            // Delivery delay (§8.1 row 1–2).
            if (project.ExpectedDeliveryDate.HasValue && project.Status == ProjectStatus.UnderConstruction)
            {
                var delayMonths = (DateTime.UtcNow - project.ExpectedDeliveryDate.Value).TotalDays / 30;

                if (delayCritical != null
                    && delayMonths > (double)delayCritical.ThresholdValue
                    && project.CompletionPercentage < 90)
                {
                    yield return Project(project, ProjectDelayCriticalKey, RiskLevel.High, AlertLevel.Level3_SeniorLeadership,
                        $"Project '{project.Name}' delivery delayed by {delayMonths:N0} months with {project.CompletionPercentage}% completion");
                }
                else if (delayWarning != null
                    && delayMonths > (double)delayWarning.ThresholdValue
                    && project.CompletionPercentage < 80)
                {
                    yield return Project(project, ProjectDelayWarningKey, RiskLevel.Warning, AlertLevel.Level1_Operational,
                        $"Project '{project.Name}' delivery delayed by {delayMonths:N0} months with {project.CompletionPercentage}% completion");
                }
            }

            // Escrow shortfall (§8.1 row 3–4).
            if (project.EscrowAccount is { } escrow)
            {
                var adequacyPct = escrow.AdequacyRatio * 100m;
                if (escrowCritical != null && adequacyPct < escrowCritical.ThresholdValue)
                {
                    yield return Project(project, EscrowShortfallCriticalKey, RiskLevel.High, AlertLevel.Level4_Strategic,
                        $"Project '{project.Name}' escrow at {escrow.AdequacyRatio:P0} — critical shortfall");
                }
                else if (escrowWarning != null && adequacyPct < escrowWarning.ThresholdValue)
                {
                    yield return Project(project, EscrowShortfallWarningKey, RiskLevel.Warning, AlertLevel.Level2_Managerial,
                        $"Project '{project.Name}' escrow at {escrow.AdequacyRatio:P0} — below minimum threshold");
                }
            }

            // Construction Activity Suspension (§8.1 row 5). Stalled projects
            // tracked for 30+ days escalate to L2; 60+ days to L3.
            if (project.Status == ProjectStatus.Stalled && suspension != null)
            {
                var stalledSince = StalledSince(project, allAlerts);
                var daysStalled  = (DateTime.UtcNow - stalledSince).TotalDays;

                if (daysStalled >= (double)suspension.ThresholdValue * 2)
                {
                    yield return Project(project, ConstructionSuspensionKey, RiskLevel.High, AlertLevel.Level3_SeniorLeadership,
                        $"Project '{project.Name}' has no construction progress for {daysStalled:N0} days");
                }
                else if (daysStalled >= (double)suspension.ThresholdValue)
                {
                    yield return Project(project, ConstructionSuspensionKey, RiskLevel.Warning, AlertLevel.Level2_Managerial,
                        $"Project '{project.Name}' has no construction progress for {daysStalled:N0} days");
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // §8.1 row 6 — Sharp Transaction Volume Decline (per zone)
    // ------------------------------------------------------------------
    private static async Task<List<RiskAlert>> EvaluateTransactionVolumeDeclineAsync(
        IServiceProvider sp, Dictionary<string, RiskThreshold> thresholdByKey)
    {
        var threshold = thresholdByKey.GetValueOrDefault(TransactionVolumeDeclineKey);
        if (threshold is null) return new();

        var now = DateTime.UtcNow;
        var lastMonthCutoff = now.AddDays(-30);
        var twelveMonthCutoff = now.AddMonths(-12);

        var txRepo = sp.GetRequiredService<IRepository<Transaction>>();
        var recent = await txRepo.Query()
            .Where(t => t.TransactionDate >= twelveMonthCutoff)
            .Select(t => new { t.ZoneId, t.TransactionDate })
            .ToListAsync();

        var alerts = new List<RiskAlert>();
        var byZone = recent.GroupBy(t => t.ZoneId);
        foreach (var zoneGroup in byZone)
        {
            var lastMonth = zoneGroup.Count(t => t.TransactionDate >= lastMonthCutoff);
            var priorElevenMonths = zoneGroup.Count(t => t.TransactionDate < lastMonthCutoff);
            if (priorElevenMonths < 11) continue; // need ~1 tx / prior month to compare

            var priorMonthlyAvg = priorElevenMonths / 11.0;
            if (priorMonthlyAvg <= 0) continue;

            var dropPct = (1.0 - lastMonth / priorMonthlyAvg) * 100;
            if (dropPct >= (double)threshold.ThresholdValue)
            {
                alerts.Add(Zone(zoneGroup.Key, TransactionVolumeDeclineKey, RiskLevel.Medium, AlertLevel.Level2_Managerial,
                    $"Monthly transactions dropped {dropPct:N0}% vs. 12-month rolling average ({lastMonth} vs {priorMonthlyAvg:N1}/mo)"));
            }
        }
        return alerts;
    }

    // ------------------------------------------------------------------
    // §8.1 row 9 — Price Decline (per zone, quarter-over-quarter)
    // ------------------------------------------------------------------
    private static async Task<List<RiskAlert>> EvaluatePriceDeclineAsync(
        IServiceProvider sp, Dictionary<string, RiskThreshold> thresholdByKey)
    {
        var threshold = thresholdByKey.GetValueOrDefault(PriceDeclineKey);
        if (threshold is null) return new();

        var now = DateTime.UtcNow;
        var currentQuarterStart = now.AddDays(-90);
        var priorQuarterStart = now.AddDays(-180);

        var txRepo = sp.GetRequiredService<IRepository<Transaction>>();
        var recent = await txRepo.Query()
            .Where(t => t.TransactionDate >= priorQuarterStart && t.PricePerSqft > 0)
            .Select(t => new { t.ZoneId, t.TransactionDate, t.PricePerSqft })
            .ToListAsync();

        var alerts = new List<RiskAlert>();
        foreach (var zoneGroup in recent.GroupBy(t => t.ZoneId))
        {
            var current = zoneGroup.Where(t => t.TransactionDate >= currentQuarterStart).ToList();
            var prior   = zoneGroup.Where(t => t.TransactionDate <  currentQuarterStart).ToList();
            if (current.Count < 5 || prior.Count < 5) continue; // guard against thin samples

            var currentAvg = current.Average(t => (double)t.PricePerSqft);
            var priorAvg   = prior.Average(t => (double)t.PricePerSqft);
            if (priorAvg <= 0) continue;

            var dropPct = (1.0 - currentAvg / priorAvg) * 100;
            if (dropPct >= (double)threshold.ThresholdValue)
            {
                alerts.Add(Zone(zoneGroup.Key, PriceDeclineKey, RiskLevel.Medium, AlertLevel.Level2_Managerial,
                    $"Average price/sqft dropped {dropPct:N0}% QoQ (AED {currentAvg:N0} vs. AED {priorAvg:N0})"));
            }
        }
        return alerts;
    }

    // ------------------------------------------------------------------
    // §8.1 row 7 — Developer Score Deterioration (quarter-over-quarter)
    // ------------------------------------------------------------------
    private static async Task<List<RiskAlert>> EvaluateDeveloperScoreDeteriorationAsync(
        IServiceProvider sp, Dictionary<string, RiskThreshold> thresholdByKey)
    {
        var threshold = thresholdByKey.GetValueOrDefault(DeveloperScoreDeteriorationKey);
        if (threshold is null) return new();

        var scoreRepo = sp.GetRequiredService<IRepository<DeveloperScore>>();
        var scores = await scoreRepo.Query()
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Quarter)
            .ToListAsync();

        var alerts = new List<RiskAlert>();
        foreach (var group in scores.GroupBy(s => s.DeveloperId))
        {
            var ordered = group.OrderByDescending(s => s.Year).ThenByDescending(s => s.Quarter).ToList();
            if (ordered.Count < 2) continue;

            var latest = ordered[0];
            var previous = ordered[1];
            var drop = previous.CompositeScore - latest.CompositeScore;
            if (drop < threshold.ThresholdValue) continue;

            // Per RFP §8.1: drop > threshold = Medium; if new score falls below 40 = High.
            var riskLevel = latest.CompositeScore < 40 ? RiskLevel.High : RiskLevel.Medium;
            var alertLevel = riskLevel == RiskLevel.High
                ? AlertLevel.Level3_SeniorLeadership
                : AlertLevel.Level2_Managerial;

            alerts.Add(Developer(group.Key, DeveloperScoreDeteriorationKey, riskLevel, alertLevel,
                $"Composite score fell {drop:N0} points (from {previous.CompositeScore:N0} to {latest.CompositeScore:N0})"));
        }
        return alerts;
    }

    // ------------------------------------------------------------------
    // §8.1 row 8 — High-Risk Project Concentration (per developer)
    // ------------------------------------------------------------------
    private static async Task<List<RiskAlert>> EvaluateHighRiskConcentrationAsync(
        IServiceProvider sp, Dictionary<string, RiskThreshold> thresholdByKey,
        List<Project> projects)
    {
        var threshold = thresholdByKey.GetValueOrDefault(HighRiskConcentrationKey);
        if (threshold is null) return new();

        // Count a project as "high risk" if it's stalled or carries any unresolved High-severity alert.
        var alertRepo = sp.GetRequiredService<IRepository<RiskAlert>>();
        var openHighAlertProjectIds = await alertRepo.Query()
            .Where(a => a.Status != AlertStatus.Resolved
                        && a.RiskLevel == RiskLevel.High
                        && a.ProjectId != null)
            .Select(a => a.ProjectId!.Value)
            .Distinct()
            .ToListAsync();
        var highRiskSet = new HashSet<Guid>(openHighAlertProjectIds);

        var alerts = new List<RiskAlert>();
        var activeProjects = projects.Where(p => p.Status != ProjectStatus.Completed).ToList();

        foreach (var devGroup in activeProjects.GroupBy(p => p.DeveloperId))
        {
            var total = devGroup.Count();
            if (total < 3) continue; // concentration metric is meaningless for tiny portfolios

            var highCount = devGroup.Count(p => p.Status == ProjectStatus.Stalled || highRiskSet.Contains(p.Id));
            var concentrationPct = (decimal)highCount / total * 100m;
            if (concentrationPct <= threshold.ThresholdValue) continue;

            alerts.Add(Developer(devGroup.Key, HighRiskConcentrationKey, RiskLevel.High, AlertLevel.Level3_SeniorLeadership,
                $"{highCount} of {total} active projects ({concentrationPct:N0}%) flagged as high-risk"));
        }
        return alerts;
    }

    // ------------------------------------------------------------------
    // §8.1 row 10 — Severe Regulatory Violation (per developer)
    // ------------------------------------------------------------------
    private static async Task<List<RiskAlert>> EvaluateSevereRegulatoryViolationAsync(
        IServiceProvider sp, Dictionary<string, RiskThreshold> thresholdByKey)
    {
        if (!thresholdByKey.ContainsKey(SevereRegulatoryViolationKey)) return new();

        // Fire once per developer per open alert cycle when they have a
        // Critical violation registered in the last 90 days.
        var cutoff = DateTime.UtcNow.AddDays(-90);
        var violationRepo = sp.GetRequiredService<IRepository<RegulatoryViolation>>();
        var recentCritical = await violationRepo.Query()
            .Where(v => v.Severity == ViolationSeverity.Critical && v.ViolationDate >= cutoff)
            .OrderByDescending(v => v.ViolationDate)
            .ToListAsync();

        return recentCritical
            .GroupBy(v => v.DeveloperId)
            .Select(g =>
            {
                var latest = g.First();
                return Developer(g.Key, SevereRegulatoryViolationKey, RiskLevel.High, AlertLevel.Level3_SeniorLeadership,
                    $"Critical RERA violation registered {latest.ViolationDate:yyyy-MM-dd}: {latest.Description}");
            })
            .ToList();
    }

    // ------------------------------------------------------------------
    // Alert factories
    // ------------------------------------------------------------------
    private static RiskAlert Project(Project project, string indicatorKey, RiskLevel riskLevel, AlertLevel alertLevel, string description)
    {
        return BuildAlert(indicatorKey, riskLevel, alertLevel, description,
            projectId: project.Id, developerId: project.DeveloperId, zoneId: project.ZoneId);
    }

    private static RiskAlert Developer(Guid developerId, string indicatorKey, RiskLevel riskLevel, AlertLevel alertLevel, string description)
        => BuildAlert(indicatorKey, riskLevel, alertLevel, description, developerId: developerId);

    private static RiskAlert Zone(Guid zoneId, string indicatorKey, RiskLevel riskLevel, AlertLevel alertLevel, string description)
        => BuildAlert(indicatorKey, riskLevel, alertLevel, description, zoneId: zoneId);

    private static RiskAlert BuildAlert(string indicatorKey, RiskLevel riskLevel, AlertLevel alertLevel, string description,
        Guid? projectId = null, Guid? developerId = null, Guid? zoneId = null)
    {
        var now = DateTime.UtcNow;
        var (ackBy, resolveBy) = AlertSla.DeadlinesFor(alertLevel, now);
        return new RiskAlert
        {
            IndicatorType = indicatorKey,
            RiskLevel = riskLevel,
            AlertLevel = alertLevel,
            Status = AlertStatus.New,
            ProjectId = projectId,
            DeveloperId = developerId,
            ZoneId = zoneId,
            Title = HumaniseIndicator(indicatorKey),
            Description = description,
            EscalationPath = $"Level {(int)alertLevel}",
            AcknowledgeDeadline = ackBy,
            ResolutionDeadline = resolveBy
        };
    }

    private static string HumaniseIndicator(string key) => key switch
    {
        ProjectDelayWarningKey         => "Project Delivery Delay — Warning",
        ProjectDelayCriticalKey        => "Project Delivery Delay — Critical",
        EscrowShortfallWarningKey      => "Escrow Shortfall — Warning",
        EscrowShortfallCriticalKey     => "Escrow Shortfall — Critical",
        ConstructionSuspensionKey      => "Construction Activity Suspension",
        TransactionVolumeDeclineKey    => "Sharp Transaction Volume Decline",
        DeveloperScoreDeteriorationKey => "Developer Score Deterioration",
        HighRiskConcentrationKey       => "High-Risk Project Concentration",
        PriceDeclineKey                => "Price Decline — Zone Level",
        SevereRegulatoryViolationKey   => "Severe Regulatory Violation",
        _                              => key
    };

    // Prefer the first time we flagged this project as stalled as the
    // "construction halted since" date; fall back to 31 days ago so a project
    // newly marked as Stalled still trips the §8.1 30-day warning immediately.
    private static DateTime StalledSince(Project project, List<RiskAlert> allAlerts)
    {
        var firstSuspension = allAlerts
            .Where(a => a.ProjectId == project.Id && a.IndicatorType == ConstructionSuspensionKey)
            .OrderBy(a => a.CreatedAt)
            .FirstOrDefault();
        return firstSuspension?.CreatedAt ?? DateTime.UtcNow.AddDays(-31);
    }
}
