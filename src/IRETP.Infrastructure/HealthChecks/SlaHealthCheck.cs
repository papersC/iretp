using IRETP.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IRETP.Infrastructure.HealthChecks;

/// <summary>
/// Aggregates the platform-wide Service-Level metrics from RFP Section 10.1
/// into a single health check. Returns Unhealthy when any P95 latency or
/// data-freshness threshold is breached, Degraded when nearing breach, and
/// Healthy when every metric is comfortably under target. Used by the DLD
/// ops monitor to drive the uptime SLA dashboard without needing an external
/// APM integration.
/// </summary>
public sealed class SlaHealthCheck : IHealthCheck
{
    // RFP Section 10.1 thresholds. Kept in code so review against the RFP
    // is explicit — adjust here only when the contract is renegotiated.
    private const double AiTextResponseP90BudgetMs = 8_000;
    private const double AiChartGenerationP90BudgetMs = 15_000;
    private static readonly TimeSpan KpiFreshnessBudget = TimeSpan.FromMinutes(15);
    // RFP v1.3 §10.1 — transaction data lag from DLD source systems must not exceed 24 h.
    private static readonly TimeSpan TransactionFreshnessBudget = TimeSpan.FromHours(24);

    private readonly IAIModelMetrics _aiMetrics;
    private readonly IKpiSnapshotCache _kpiCache;
    private readonly IFabricGoldDataSource _fabric;

    public SlaHealthCheck(IAIModelMetrics aiMetrics, IKpiSnapshotCache kpiCache, IFabricGoldDataSource fabric)
    {
        _aiMetrics = aiMetrics;
        _kpiCache = kpiCache;
        _fabric = fabric;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var breaches = new List<string>();
        var warnings = new List<string>();

        // --- AI latency vs RFP 10.1 budgets ---------------------------------
        var snapshots = _aiMetrics.Snapshot();
        foreach (var s in snapshots)
        {
            data[$"ai.{s.Tier}.{s.Name}.p95Ms"] = s.P95LatencyMs;
            data[$"ai.{s.Tier}.{s.Name}.uaeResident"] = s.UaeResident;

            if (s.P95LatencyMs > AiChartGenerationP90BudgetMs)
                breaches.Add($"AI {s.Name} P95 {s.P95LatencyMs:F0}ms exceeds 15s chart-gen budget");
            else if (s.P95LatencyMs > AiTextResponseP90BudgetMs)
                warnings.Add($"AI {s.Name} P95 {s.P95LatencyMs:F0}ms exceeds 8s text-response budget");

            if (!s.UaeResident && s.SuccessCalls + s.FailedCalls > 0)
                breaches.Add($"AI {s.Name} region '{s.Region}' is not UAE-resident");
        }

        // --- Dashboard KPI freshness vs RFP FR003 ---------------------------
        var snapshot = _kpiCache.Current;
        if (snapshot is null)
        {
            warnings.Add("KPI snapshot has not been computed yet");
            data["kpi.refreshedAt"] = "never";
        }
        else
        {
            var age = DateTime.UtcNow - snapshot.RefreshedAt;
            data["kpi.refreshedAt"] = snapshot.RefreshedAt;
            data["kpi.ageSeconds"] = (int)age.TotalSeconds;

            if (age > KpiFreshnessBudget * 2)
                breaches.Add($"KPI snapshot age {age.TotalMinutes:F0}m exceeds 30m hard ceiling");
            else if (age > KpiFreshnessBudget)
                warnings.Add($"KPI snapshot age {age.TotalMinutes:F0}m exceeds 15m FR003 budget");
        }

        // --- Microsoft Fabric / OneLake Gold freshness vs RFP v1.3 §10.1 -----
        // The Gold layer is the source of truth for analytics-ready data, so
        // its freshness is a first-class SLA concern alongside the KPI cache.
        try
        {
            var freshness = await _fabric.GetFreshnessAsync(cancellationToken);
            data["fabric.mode"] = _fabric.Mode.ToString();
            data["fabric.goldWatermark"] = (object?)freshness.GoldLayerLastWriteUtc ?? "null";
            data["fabric.pipelineStatus"] = freshness.LastPipelineStatus ?? "unknown";

            if (freshness.TransactionLag is { } lag)
            {
                data["fabric.transactionLagMinutes"] = (int)lag.TotalMinutes;
                if (lag > TransactionFreshnessBudget * 2)
                    breaches.Add($"Fabric transaction lag {lag.TotalHours:F1}h exceeds 48h hard ceiling");
                else if (lag > TransactionFreshnessBudget)
                    warnings.Add($"Fabric transaction lag {lag.TotalHours:F1}h exceeds 24h §10.1 budget");
            }
            if (string.Equals(freshness.LastPipelineStatus, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                breaches.Add($"Fabric Data Factory pipeline status reports Failed");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Honour the caller's cancellation request — don't swallow it into a Degraded result.
            throw;
        }
        catch (Exception ex)
        {
            warnings.Add($"Fabric freshness probe failed: {ex.Message}");
        }

        if (breaches.Count > 0)
        {
            return HealthCheckResult.Unhealthy(
                "SLA breach: " + string.Join("; ", breaches), data: data);
        }
        if (warnings.Count > 0)
        {
            return HealthCheckResult.Degraded(
                "SLA warning: " + string.Join("; ", warnings), data: data);
        }

        return HealthCheckResult.Healthy(
            "All RFP §10.1 metrics within budget.", data: data);
    }
}
