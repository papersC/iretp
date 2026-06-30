namespace IRETP.Web.Services;

/// <summary>
/// Demo-mode fallback data for the Internal Platform admin pages (EWRS,
/// Escrow monitoring, Developer rating). Used when the AdminAPI is unreachable
/// so the UAT audience can still exercise the UI end-to-end. Production
/// disables this path — admin pages then always hit the authenticated API.
/// </summary>
public class AdminFixtureService
{
    public EwrsDashboardData EwrsDashboard() => new()
    {
        TotalHighRiskProjects = 4,
        TotalWarningProjects = 11,
        ProjectsWithEscrowShortfall = 3,
        ProjectsWithConstructionHalt = 2,
        TotalActiveAlerts = 18,
        UnacknowledgedAlerts = 7,
        ZoneRiskSummary = new()
        {
            new() { ZoneName = "Dubailand",             HighRiskCount = 2, WarningCount = 5, TotalProjects = 12 },
            new() { ZoneName = "Business Bay",          HighRiskCount = 1, WarningCount = 3, TotalProjects = 9  },
            new() { ZoneName = "Palm Jebel Ali",        HighRiskCount = 1, WarningCount = 1, TotalProjects = 4  },
            new() { ZoneName = "Meydan",                HighRiskCount = 0, WarningCount = 2, TotalProjects = 7  },
            new() { ZoneName = "Dubai Creek Harbour",   HighRiskCount = 0, WarningCount = 0, TotalProjects = 5  },
        },
        RecentAlerts = new()
        {
            new() { Id = Guid.NewGuid(), IndicatorType = "EscrowShortfall", RiskLevel = 2, AlertLevel = 2,
                    Title = "Escrow balance below 60% — Deyaar Mayan",
                    CreatedAt = DateTime.UtcNow.AddHours(-3) },
            new() { Id = Guid.NewGuid(), IndicatorType = "ConstructionHalt", RiskLevel = 2, AlertLevel = 2,
                    Title = "No progress > 60 days — Palm Jebel Ali Villas",
                    CreatedAt = DateTime.UtcNow.AddHours(-17) },
            new() { Id = Guid.NewGuid(), IndicatorType = "PriceDecline", RiskLevel = 1, AlertLevel = 2,
                    Title = "Price -16% QoQ — Dubai Silicon Oasis",
                    CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new() { Id = Guid.NewGuid(), IndicatorType = "DeliveryDelay", RiskLevel = 1, AlertLevel = 1,
                    Title = "Delivery +8 months — Azizi Riviera",
                    CreatedAt = DateTime.UtcNow.AddDays(-2) },
            new() { Id = Guid.NewGuid(), IndicatorType = "TransactionDecline", RiskLevel = 1, AlertLevel = 2,
                    Title = "Txn volume -42% — Al Barsha",
                    CreatedAt = DateTime.UtcNow.AddDays(-3) },
        }
    };

    /// <summary>Full alert list for the /admin/ewrs/alerts workflow page.</summary>
    public List<RiskAlertItem> FullAlertList()
    {
        var now = DateTime.UtcNow;
        // Deadlines come from IRETP.Domain.Common.AlertSla — applied here so
        // the demo path mirrors what the live engine would persist.
        static (DateTime?, DateTime?) Deadlines(int level, DateTime created) => level switch
        {
            1 => (created.AddHours(4), created.AddDays(2)),
            2 => (created.AddHours(2), created.AddDays(1)),
            3 => (created.AddHours(1), created.AddHours(4)),
            _ => (null, null) // L4 immediate
        };
        RiskAlertItem Build(string ind, int risk, int level, int status, string title, string desc,
            DateTime created, string? ackBy = null, DateTime? ackAt = null, bool autoEscalated = false)
        {
            var (ackBy_, resBy) = Deadlines(level, created);
            return new()
            {
                Id = Guid.NewGuid(),
                IndicatorType = ind, RiskLevel = risk, AlertLevel = level, Status = status,
                Title = title, Description = desc,
                CreatedAt = created, AcknowledgedBy = ackBy, AcknowledgedAt = ackAt,
                AcknowledgeDeadline = ackBy_,
                ResolutionDeadline = resBy,
                AutoEscalated = autoEscalated
            };
        }
        return new()
        {
            Build("EscrowShortfall", 2, 3, 0, "Escrow balance 39% — Deyaar Mayan",
                "Escrow balance dropped to 38.6% of required minimum ({cur} AED vs {req} AED).",
                now.AddHours(-3)),
            Build("ConstructionHalt", 2, 2, 0, "No progress > 60 days — Palm Jebel Ali Villas",
                "No construction update registered in 62 days. Escalated to Section Manager.",
                now.AddHours(-17), autoEscalated: true),
            Build("PriceDecline", 1, 2, 1, "Price -16% QoQ — Dubai Silicon Oasis",
                "Average price per sqft declined 16% vs prior quarter across 512 transactions.",
                now.AddDays(-1), "a.ali@dld.gov.ae", now.AddHours(-20)),
            Build("DeliveryDelay", 1, 1, 1, "Delivery +8 months — Azizi Riviera",
                "Declared delivery exceeded by 8 months at 72% completion.",
                now.AddDays(-2), "m.hashem@dld.gov.ae", now.AddDays(-1)),
            Build("TransactionDecline", 1, 2, 0, "Txn volume -42% — Al Barsha",
                "Monthly transactions in zone dropped 42% vs 12-month rolling avg.",
                now.AddDays(-3), autoEscalated: true),
            Build("DeveloperScoreDrop", 1, 2, 1, "Binghatti score -6 QoQ",
                "Developer composite score fell from 55 to 49 this quarter.",
                now.AddDays(-5), "r.malik@dld.gov.ae", now.AddDays(-4)),
            Build("EscrowShortfall", 1, 1, 2, "Escrow 92% — Creek Horizon (resolved)",
                "Escrow dipped briefly below 95%; replenished by developer within 24h.",
                now.AddDays(-6), "a.ali@dld.gov.ae", now.AddDays(-6)),
            Build("HighRiskConcentration", 2, 3, 0, "High-risk portfolio > 30% — Dubailand",
                "Share of High Risk projects exceeded 30% (currently 34%).",
                now.AddDays(-8), autoEscalated: true),
        };
    }

    public List<RiskThresholdItem> Thresholds() => new()
    {
        new() { IndicatorKey = "delivery_delay_warn",    IndicatorName = "Delivery delay (warn)",   ThresholdValue =  6, ThresholdUnit = "months",  DefaultRiskLevel = 1, DefaultAlertLevel = 1 },
        new() { IndicatorKey = "delivery_delay_crit",    IndicatorName = "Delivery delay (crit)",   ThresholdValue = 12, ThresholdUnit = "months",  DefaultRiskLevel = 2, DefaultAlertLevel = 3 },
        new() { IndicatorKey = "escrow_shortfall_warn",  IndicatorName = "Escrow shortfall (warn)", ThresholdValue = 80, ThresholdUnit = "% of req", DefaultRiskLevel = 1, DefaultAlertLevel = 1 },
        new() { IndicatorKey = "escrow_shortfall_crit",  IndicatorName = "Escrow shortfall (crit)", ThresholdValue = 60, ThresholdUnit = "% of req", DefaultRiskLevel = 2, DefaultAlertLevel = 3 },
        new() { IndicatorKey = "construction_halt_warn", IndicatorName = "Construction halt (warn)",ThresholdValue = 30, ThresholdUnit = "days",    DefaultRiskLevel = 1, DefaultAlertLevel = 2 },
        new() { IndicatorKey = "construction_halt_crit", IndicatorName = "Construction halt (crit)",ThresholdValue = 60, ThresholdUnit = "days",    DefaultRiskLevel = 2, DefaultAlertLevel = 3 },
        new() { IndicatorKey = "txn_volume_decline",     IndicatorName = "Txn volume decline",      ThresholdValue = 40, ThresholdUnit = "% MoM",   DefaultRiskLevel = 1, DefaultAlertLevel = 2 },
        new() { IndicatorKey = "dev_score_drop",         IndicatorName = "Developer score drop",    ThresholdValue = 15, ThresholdUnit = "points",  DefaultRiskLevel = 1, DefaultAlertLevel = 2 },
        new() { IndicatorKey = "price_decline_zone",     IndicatorName = "Price decline (zone)",    ThresholdValue = 15, ThresholdUnit = "% QoQ",   DefaultRiskLevel = 1, DefaultAlertLevel = 2 },
        new() { IndicatorKey = "severe_rera_violation", IndicatorName = "Severe RERA violation",   ThresholdValue =  1, ThresholdUnit = "count",   DefaultRiskLevel = 2, DefaultAlertLevel = 3 },
    };

    public List<EscrowItem> EscrowDashboard() => new()
    {
        new() { ProjectName = "Emaar Beachfront",        DeveloperName = "Emaar Properties",   BankName = "Emirates NBD Escrow",
                CurrentBalance = 2_450_000_000m, RequiredMinimumBalance = 2_300_000_000m, AdequacyRatio = 1.065m, Status = 0 },
        new() { ProjectName = "Creek Horizon",           DeveloperName = "Emaar Properties",   BankName = "Dubai Islamic Bank",
                CurrentBalance = 1_150_000_000m, RequiredMinimumBalance = 1_250_000_000m, AdequacyRatio = 0.92m,  Status = 1 },
        new() { ProjectName = "Damac Lagoons",           DeveloperName = "DAMAC",              BankName = "ADCB Escrow",
                CurrentBalance = 2_850_000_000m, RequiredMinimumBalance = 3_000_000_000m, AdequacyRatio = 0.95m,  Status = 1 },
        new() { ProjectName = "Palm Jebel Ali Villas",   DeveloperName = "Nakheel",            BankName = "Mashreq Escrow",
                CurrentBalance =   290_000_000m, RequiredMinimumBalance =   520_000_000m, AdequacyRatio = 0.558m, Status = 2 },
        new() { ProjectName = "Sobha Hartland Greens",   DeveloperName = "Sobha Realty",       BankName = "HSBC Escrow",
                CurrentBalance =   780_000_000m, RequiredMinimumBalance =   700_000_000m, AdequacyRatio = 1.114m, Status = 0 },
        new() { ProjectName = "Azizi Riviera",           DeveloperName = "Azizi Developments", BankName = "FAB Escrow",
                CurrentBalance = 1_980_000_000m, RequiredMinimumBalance = 2_050_000_000m, AdequacyRatio = 0.966m, Status = 1 },
        new() { ProjectName = "Binghatti Canal",         DeveloperName = "Binghatti",          BankName = "RAKBank Escrow",
                CurrentBalance =   195_000_000m, RequiredMinimumBalance =   360_000_000m, AdequacyRatio = 0.542m, Status = 2 },
        new() { ProjectName = "Omniyat Orla",            DeveloperName = "Omniyat",            BankName = "Standard Chartered",
                CurrentBalance =   640_000_000m, RequiredMinimumBalance =   600_000_000m, AdequacyRatio = 1.067m, Status = 0 },
        new() { ProjectName = "Deyaar Mayan",            DeveloperName = "Deyaar",             BankName = "ENBD Islamic",
                CurrentBalance =    85_000_000m, RequiredMinimumBalance =   220_000_000m, AdequacyRatio = 0.386m, Status = 2 },
    };

    /// <summary>Per-project escrow transaction audit trail (read-only).</summary>
    public List<EscrowAuditEntry> EscrowAuditLog(string projectName)
    {
        var rng = new Random(projectName.Sum(c => (int)c));
        var start = new DateTime(2026, 1, 1);
        var entries = new List<EscrowAuditEntry>();
        var balance = 2_000_000_000m + rng.Next(-200_000_000, 500_000_000);
        for (var i = 0; i < 18; i++)
        {
            var at = start.AddDays(i * 6 + rng.Next(0, 3)).AddHours(rng.Next(9, 17));
            var isDeposit = rng.NextDouble() > 0.38;
            var amount = (decimal)(rng.NextDouble() * 120_000_000 + 8_000_000);
            balance += isDeposit ? amount : -amount;
            entries.Add(new EscrowAuditEntry(
                at,
                isDeposit ? "Buyer deposit" : (rng.NextDouble() > 0.5 ? "Authorized withdrawal" : "Milestone release"),
                isDeposit ? amount : -amount,
                balance,
                isDeposit ? "Buyer installment (escrow account)" : "Bank-approved milestone drawdown",
                "RERA-" + (50000 + rng.Next(10000)).ToString()
            ));
        }
        return entries.OrderByDescending(e => e.At).ToList();
    }

    public sealed record EscrowAuditEntry(
        DateTime At,
        string Type,
        decimal Amount,
        decimal BalanceAfter,
        string Description,
        string ReferenceNumber);

    public List<ScoringWeightItem> ScoringWeights() => new()
    {
        new() { CriterionKey = "on_time_delivery",        CriterionName = "On-Time Project Delivery",     Weight = 25 },
        new() { CriterionKey = "unit_sales_completion",   CriterionName = "Unit Sales Completion Rate",   Weight = 20 },
        new() { CriterionKey = "escrow_health",           CriterionName = "Escrow Account Health",        Weight = 20 },
        new() { CriterionKey = "regulatory_compliance",   CriterionName = "Regulatory Compliance",        Weight = 15 },
        new() { CriterionKey = "financial_soundness",     CriterionName = "Financial Soundness",          Weight = 10 },
        new() { CriterionKey = "historical_success",      CriterionName = "Historical Project Success",   Weight = 10 },
    };

    public List<DeveloperLeaderboardRow> DeveloperLeaderboard() => new()
    {
        new("Emaar Properties",    92, "Low",    +3, 48, 42, 125_400, 99.2m),
        new("Meraas",              87, "Low",    +1, 19, 17,  38_200, 98.1m),
        new("Sobha Realty",        84, "Low",    +2, 22, 18,  41_800, 97.6m),
        new("Nakheel",             81, "Low",    -1, 31, 27,  63_100, 95.8m),
        new("DAMAC",               76, "Medium",  0, 52, 44, 108_900, 94.4m),
        new("Azizi Developments",  71, "Medium", +2, 24, 20,  47_500, 92.7m),
        new("Ellington Properties",69, "Medium", -1, 15, 13,  18_600, 96.3m),
        new("Omniyat",             66, "Medium", +1, 11,  8,   7_400, 90.1m),
        new("Deyaar",              54, "High",   -4, 14,  9,  22_100, 78.4m),
        new("Binghatti",           49, "High",   -6, 20, 13,  31_500, 74.9m),
    };

    public sealed record DeveloperLeaderboardRow(
        string Name,
        int Score,
        string RiskBand,
        int TrendDelta,
        int TotalProjects,
        int Completed,
        int UnitsDelivered,
        decimal OnTimeRatePct)
    {
        /// <summary>
        /// Deterministic guid derived from the developer name so fixture rows
        /// can be used interchangeably with API responses (e.g. the comparison
        /// picker). Matches the seed convention used in DbSeeder.
        /// </summary>
        public Guid Id => DeterministicGuid(Name);

        private static Guid DeterministicGuid(string input)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return new Guid(hash);
        }
    }
}
