using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IRETP.Infrastructure.HealthChecks;

/// <summary>
/// Enforces RFP §6.2 delivery SLAs end-to-end by measuring real delivery
/// latency on the <see cref="Notification"/> table for the last hour:
/// <list type="bullet">
///   <item>Email within 5 minutes of trigger.</item>
///   <item>SMS within 3 minutes of trigger.</item>
///   <item>In-platform instantaneous (we accept anything under 30 s).</item>
/// </list>
/// Returns <see cref="HealthStatus.Degraded"/> when P95 exceeds budget and
/// <see cref="HealthStatus.Unhealthy"/> once double the budget is crossed —
/// matching the rest of the §10.1 SLO monitoring pattern.
/// </summary>
public sealed class NotificationSlaHealthCheck : IHealthCheck
{
    // RFP §6.2 budgets.
    private static readonly TimeSpan EmailBudget      = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SmsBudget        = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan InPlatformBudget = TimeSpan.FromSeconds(30);

    // Latency sampling window — cheap DB query, scoped to the last hour so
    // the probe stays fast even on busy days.
    private static readonly TimeSpan SampleWindow = TimeSpan.FromHours(1);

    private readonly IRepository<Notification> _repo;

    public NotificationSlaHealthCheck(IRepository<Notification> repo)
    {
        _repo = repo;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow - SampleWindow;
        var recent = await _repo.Query()
            .Where(n => n.IsSent && n.SentAt != null && n.CreatedAt >= since)
            .Select(n => new { n.Channel, n.CreatedAt, SentAt = n.SentAt!.Value })
            .ToListAsync(cancellationToken);

        var data = new Dictionary<string, object>
        {
            ["window.hours"] = SampleWindow.TotalHours,
            ["sampleCount"]  = recent.Count
        };
        var breaches = new List<string>();
        var warnings = new List<string>();

        void EvaluateChannel(string channel, TimeSpan budget)
        {
            var latencies = recent
                .Where(n => string.Equals(n.Channel, channel, StringComparison.OrdinalIgnoreCase))
                .Select(n => (n.SentAt - n.CreatedAt).TotalMilliseconds)
                .OrderBy(ms => ms)
                .ToList();

            if (latencies.Count == 0)
            {
                data[$"{channel}.sampleCount"] = 0;
                return;
            }

            var p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
            var p95 = latencies[Math.Clamp(p95Index, 0, latencies.Count - 1)];
            var median = latencies[latencies.Count / 2];

            data[$"{channel}.sampleCount"]  = latencies.Count;
            data[$"{channel}.medianMs"]     = (int)median;
            data[$"{channel}.p95Ms"]        = (int)p95;
            data[$"{channel}.budgetMs"]     = (int)budget.TotalMilliseconds;

            var budgetMs = budget.TotalMilliseconds;
            if (p95 > budgetMs * 2)
                breaches.Add($"{channel} P95 {p95 / 1000:F1}s exceeds 2× {budget.TotalMinutes:F0}-min SLA");
            else if (p95 > budgetMs)
                warnings.Add($"{channel} P95 {p95 / 1000:F1}s exceeds {budget.TotalMinutes:F0}-min SLA");
        }

        EvaluateChannel("Email",      EmailBudget);
        EvaluateChannel("SMS",        SmsBudget);
        EvaluateChannel("InPlatform", InPlatformBudget);

        if (breaches.Count > 0)
            return HealthCheckResult.Unhealthy(
                "Notification SLA breach (§6.2): " + string.Join("; ", breaches), data: data);
        if (warnings.Count > 0)
            return HealthCheckResult.Degraded(
                "Notification SLA warning (§6.2): " + string.Join("; ", warnings), data: data);
        return HealthCheckResult.Healthy(
            "Notification delivery latencies within §6.2 budgets.", data: data);
    }
}
