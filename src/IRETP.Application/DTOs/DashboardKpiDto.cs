namespace IRETP.Application.DTOs;

public class DashboardKpiDto
{
    public KpiMetric TotalTransactionsCount { get; set; } = default!;
    public KpiMetric TotalTransactionsValue { get; set; } = default!;
    public KpiMetric AveragePricePerSqft { get; set; } = default!;
    public KpiMetric CompletedUnitsYtd { get; set; } = default!;
    public KpiMetric ActiveOffPlanProjects { get; set; } = default!;
    public KpiMetric AverageRentalYield { get; set; } = default!;

    /// <summary>
    /// When the snapshot was last computed. Cached and refreshed every 15
    /// minutes by the KPI snapshot job (RFP FR003). The frontend can show
    /// "as of HH:mm" to satisfy the data-freshness disclosure requirement.
    /// </summary>
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;
}

public class KpiMetric
{
    public decimal Value { get; set; }
    public decimal? TrendMoM { get; set; }
    public decimal? TrendYoY { get; set; }
}
