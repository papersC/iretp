namespace IRETP.Application.Interfaces;

/// <summary>
/// Deterministic time-series analytics that the AI Agent calls into for
/// the "deep data analysis" capability (RFP AI004 — correlation analysis,
/// anomaly detection, multi-year trend analysis). Keeping these out of the
/// LLM ensures statistical claims are reproducible and audit-traceable.
/// </summary>
public interface ITimeSeriesAnalyzer
{
    /// <summary>
    /// Flags monthly observations whose absolute z-score against the rest of
    /// the series exceeds <paramref name="zThreshold"/>. The first window
    /// (default 3) is excluded from anomaly detection because its baseline
    /// is too thin to be reliable.
    /// </summary>
    IReadOnlyList<TimeSeriesAnomaly> DetectAnomalies(
        IReadOnlyList<TimeSeriesPoint> series,
        double zThreshold = 2.5,
        int warmUpWindow = 3);

    /// <summary>
    /// Returns the slope, R², and direction for a simple least-squares
    /// linear fit against the series. Always labelled as historical — the
    /// AI Agent must not present this as a prediction.
    /// </summary>
    TrendSummary AnalyzeTrend(IReadOnlyList<TimeSeriesPoint> series);

    /// <summary>
    /// Pearson correlation coefficient between two equal-length series
    /// (returns null when length &lt; 3 or either series has zero variance).
    /// </summary>
    double? Correlate(IReadOnlyList<double> x, IReadOnlyList<double> y);
}

public sealed record TimeSeriesPoint(DateTime Period, double Value);

public sealed record TimeSeriesAnomaly(
    DateTime Period,
    double Value,
    double ZScore,
    AnomalyDirection Direction);

public enum AnomalyDirection { Spike, Drop }

public sealed record TrendSummary(
    double Slope,
    double Intercept,
    double RSquared,
    TrendDirection Direction);

public enum TrendDirection { Increasing, Decreasing, Flat }
