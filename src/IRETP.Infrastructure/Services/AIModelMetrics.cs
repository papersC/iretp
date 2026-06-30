using System.Collections.Concurrent;
using IRETP.Application.Interfaces;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// In-memory implementation of <see cref="IAIModelMetrics"/>. Thread-safe,
/// bounded history window (last 200 latencies per model). Designed to be
/// registered as a singleton so counters survive across scoped orchestrator
/// instances for the life of the process.
/// </summary>
public class AIModelMetrics : IAIModelMetrics
{
    private const int LatencySampleCap = 200;
    private readonly ConcurrentDictionary<string, ModelState> _states = new();

    public void RecordSuccess(string modelName, double latencyMs)
    {
        var state = _states.GetOrAdd(modelName, CreateState);
        lock (state)
        {
            state.Success++;
            state.Latencies.Add(latencyMs);
            if (state.Latencies.Count > LatencySampleCap)
            {
                state.Latencies.RemoveAt(0);
            }
            state.LastCalledAt = DateTime.UtcNow;
            state.FallbackActive = false;
        }
    }

    public void RecordFailure(string modelName, string? errorMessage)
    {
        var state = _states.GetOrAdd(modelName, CreateState);
        lock (state)
        {
            state.Failure++;
            state.LastError = errorMessage;
            state.LastCalledAt = DateTime.UtcNow;
            state.FallbackActive = true;
        }
    }

    public void SetActive(string modelName, bool active)
    {
        var state = _states.GetOrAdd(modelName, CreateState);
        lock (state)
        {
            state.Active = active;
        }
    }

    public void SetMetadata(string modelName, string tier, string region)
    {
        var state = _states.GetOrAdd(modelName, CreateState);
        lock (state)
        {
            state.Tier = tier;
            state.Region = region;
        }
    }

    public IReadOnlyList<AIModelSnapshot> Snapshot()
    {
        return _states.Select(kv =>
        {
            lock (kv.Value)
            {
                var latencies = kv.Value.Latencies.ToList();
                var avg = latencies.Count > 0 ? latencies.Average() : 0d;
                var p95 = latencies.Count > 0
                    ? Percentile(latencies, 0.95)
                    : 0d;

                return new AIModelSnapshot
                {
                    Name = kv.Key,
                    Version = kv.Value.Version,
                    Tier = kv.Value.Tier,
                    Region = kv.Value.Region,
                    UaeResident = kv.Value.Region.Contains("UAE", StringComparison.OrdinalIgnoreCase)
                                  || kv.Value.Region.Contains("uae-", StringComparison.OrdinalIgnoreCase),
                    Active = kv.Value.Active,
                    SuccessCalls = kv.Value.Success,
                    FailedCalls = kv.Value.Failure,
                    AverageLatencyMs = Math.Round(avg, 1),
                    P95LatencyMs = Math.Round(p95, 1),
                    LastCalledAt = kv.Value.LastCalledAt,
                    LastError = kv.Value.LastError,
                    FallbackActive = kv.Value.FallbackActive
                };
            }
        })
        .OrderByDescending(s => s.SuccessCalls + s.FailedCalls)
        .ToList();
    }

    private static ModelState CreateState(string name) => new()
    {
        Name = name,
        Active = true,
        Version = "1.0"
    };

    private static double Percentile(List<double> sorted, double p)
    {
        sorted.Sort();
        var index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private sealed class ModelState
    {
        public string Name { get; init; } = default!;
        public string Version { get; set; } = "1.0";
        public string Tier { get; set; } = "—";
        public string Region { get; set; } = "—";
        public bool Active { get; set; }
        public long Success { get; set; }
        public long Failure { get; set; }
        public List<double> Latencies { get; } = new();
        public DateTime? LastCalledAt { get; set; }
        public string? LastError { get; set; }
        public bool FallbackActive { get; set; }
    }
}
