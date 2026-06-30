namespace IRETP.Application.Interfaces;

/// <summary>
/// Runs a standardised question set through the AI Agent and scores the
/// results so DLD can validate the &gt;= 90% accuracy SLA from RFP AI001.
/// Implementations are not expected to persist runs — the admin endpoint
/// returns the report JSON for the caller to archive.
/// </summary>
public interface IAiAccuracyHarness
{
    /// <summary>
    /// Execute every question in the catalog (or a single language slice if
    /// <paramref name="language"/> is supplied) and return the aggregated
    /// report.
    /// </summary>
    Task<AiAccuracyReport> RunAsync(string? language = null, CancellationToken ct = default);
}

public sealed class AiAccuracyReport
{
    public DateTime RanAtUtc { get; init; } = DateTime.UtcNow;
    public string? Language { get; init; }
    public int TotalQuestions { get; init; }
    public int PassedCount { get; init; }
    public int FailedCount { get; init; }
    public int RefusalCount { get; init; }
    public decimal AccuracyPct { get; init; }
    public bool MeetsSla => AccuracyPct >= 90m;
    public long TotalLatencyMs { get; init; }
    public List<AiAccuracyQuestionResult> Results { get; init; } = [];
}

public sealed class AiAccuracyQuestionResult
{
    public string Id { get; init; } = default!;
    public string Question { get; init; } = default!;
    public string Language { get; init; } = "en";
    public string Topic { get; init; } = default!;
    public IReadOnlyList<string> ExpectedKeywords { get; init; } = [];
    public IReadOnlyList<string> MatchedKeywords { get; init; } = [];
    public string Answer { get; init; } = default!;
    public bool Passed { get; init; }
    public bool WasRefusal { get; init; }
    public long LatencyMs { get; init; }
}
