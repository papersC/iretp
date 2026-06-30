namespace IRETP.Application.Interfaces;

/// <summary>
/// Lightweight in-process metric recorder for AI model performance (RFP 5.3
/// Model Performance Transparency). Accumulates per-model counters so the
/// admin UI can render a status table without needing an external APM.
/// Resets on process restart — the admin API reads these counters directly.
/// </summary>
public interface IAIModelMetrics
{
    void RecordSuccess(string modelName, double latencyMs);
    void RecordFailure(string modelName, string? errorMessage);
    void SetActive(string modelName, bool active);

    /// <summary>
    /// Decorate a model entry with deployment metadata (tier role, hosting
    /// region) so the admin transparency panel can show "primary, hosted in
    /// UAE-North" alongside latency.
    /// </summary>
    void SetMetadata(string modelName, string tier, string region);

    IReadOnlyList<AIModelSnapshot> Snapshot();
}

public sealed class AIModelSnapshot
{
    public string Name { get; init; } = default!;
    public string Version { get; init; } = "—";
    public string Tier { get; set; } = "—";
    public string Region { get; set; } = "—";
    public bool UaeResident { get; set; }
    public bool Active { get; set; }
    public long SuccessCalls { get; set; }
    public long FailedCalls { get; set; }
    public double AverageLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public DateTime? LastCalledAt { get; set; }
    public string? LastError { get; set; }
    public bool FallbackActive { get; set; }
}
