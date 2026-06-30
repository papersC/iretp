using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IRETP.WebAPI.HealthChecks;

/// <summary>
/// Reports Hangfire server + queue state. Returns Degraded when no servers
/// heartbeat within the last 5 minutes (workers might have crashed) or when
/// the "failed" jobs count exceeds a configurable ceiling. Returns Unhealthy
/// only when the storage layer itself is unreachable — callers should still
/// get a 200 when workers are merely slow.
/// </summary>
public sealed class HangfireHealthCheck : IHealthCheck
{
    private const int FailedJobsCeiling = 50;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var monitor = JobStorage.Current.GetMonitoringApi();
            var servers = monitor.Servers();
            var stats = monitor.GetStatistics();

            var data = new Dictionary<string, object>
            {
                ["servers"] = servers.Count,
                ["enqueued"] = stats.Enqueued,
                ["scheduled"] = stats.Scheduled,
                ["processing"] = stats.Processing,
                ["failed"] = stats.Failed,
                ["succeeded"] = stats.Succeeded,
                ["recurring"] = stats.Recurring
            };

            if (servers.Count == 0)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "No Hangfire servers registered — recurring jobs will not run.", data: data));
            }

            var mostRecentHeartbeat = servers.Max(s => s.Heartbeat ?? s.StartedAt);
            if (mostRecentHeartbeat < DateTime.UtcNow.AddMinutes(-5))
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    "Hangfire server last heartbeat is older than 5 minutes.", data: data));
            }

            if (stats.Failed > FailedJobsCeiling)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"{stats.Failed} failed jobs accumulated (ceiling {FailedJobsCeiling}).", data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy("Hangfire is running.", data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Hangfire storage is unreachable.", ex));
        }
    }
}
