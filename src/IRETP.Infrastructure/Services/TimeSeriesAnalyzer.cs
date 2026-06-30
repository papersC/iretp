using IRETP.Application.Interfaces;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Z-score anomaly detection + least-squares trend + Pearson correlation.
/// Deterministic and dependency-free so the AI Agent can quote stats without
/// relying on the LLM's arithmetic. RFP AI004 — deep data analysis.
/// </summary>
public class TimeSeriesAnalyzer : ITimeSeriesAnalyzer
{
    public IReadOnlyList<TimeSeriesAnomaly> DetectAnomalies(
        IReadOnlyList<TimeSeriesPoint> series,
        double zThreshold = 2.5,
        int warmUpWindow = 3)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count <= warmUpWindow) return Array.Empty<TimeSeriesAnomaly>();

        var values = series.Select(p => p.Value).ToArray();
        var mean = values.Average();
        var variance = values.Average(v => (v - mean) * (v - mean));
        var stdDev = Math.Sqrt(variance);

        // Constant series have no anomalies — bail before dividing by zero.
        if (stdDev == 0d) return Array.Empty<TimeSeriesAnomaly>();

        var anomalies = new List<TimeSeriesAnomaly>();
        for (var i = warmUpWindow; i < series.Count; i++)
        {
            var z = (series[i].Value - mean) / stdDev;
            if (Math.Abs(z) >= zThreshold)
            {
                anomalies.Add(new TimeSeriesAnomaly(
                    series[i].Period,
                    series[i].Value,
                    Math.Round(z, 3),
                    z >= 0 ? AnomalyDirection.Spike : AnomalyDirection.Drop));
            }
        }
        return anomalies;
    }

    public TrendSummary AnalyzeTrend(IReadOnlyList<TimeSeriesPoint> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.Count < 2)
            return new TrendSummary(0, series.Count == 1 ? series[0].Value : 0, 0, TrendDirection.Flat);

        // X = ordinal index (0..n-1) so trend is "per period" regardless of
        // calendar gaps between points. Callers should ensure the series is
        // already evenly spaced (e.g. monthly aggregations).
        var n = series.Count;
        var xs = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
        var ys = series.Select(p => p.Value).ToArray();

        var meanX = xs.Average();
        var meanY = ys.Average();

        double numerator = 0, denomX = 0, denomYSquared = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = xs[i] - meanX;
            var dy = ys[i] - meanY;
            numerator += dx * dy;
            denomX += dx * dx;
            denomYSquared += dy * dy;
        }

        var slope = denomX == 0 ? 0 : numerator / denomX;
        var intercept = meanY - slope * meanX;
        var rSquared = denomYSquared == 0
            ? 0
            : Math.Pow(numerator, 2) / (denomX * denomYSquared);

        // 0.5% per period is the threshold for "flat" — anything narrower
        // is rounding noise on real data.
        var relativeSlope = meanY == 0 ? slope : slope / Math.Abs(meanY);
        var direction = Math.Abs(relativeSlope) < 0.005
            ? TrendDirection.Flat
            : slope > 0 ? TrendDirection.Increasing : TrendDirection.Decreasing;

        return new TrendSummary(
            Math.Round(slope, 4),
            Math.Round(intercept, 4),
            Math.Round(rSquared, 4),
            direction);
    }

    public double? Correlate(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (x.Count != y.Count) throw new ArgumentException("Series must be the same length.");
        if (x.Count < 3) return null;

        var meanX = x.Average();
        var meanY = y.Average();

        double numerator = 0, denomX = 0, denomY = 0;
        for (var i = 0; i < x.Count; i++)
        {
            var dx = x[i] - meanX;
            var dy = y[i] - meanY;
            numerator += dx * dy;
            denomX += dx * dx;
            denomY += dy * dy;
        }

        if (denomX == 0 || denomY == 0) return null;
        return Math.Round(numerator / Math.Sqrt(denomX * denomY), 4);
    }
}
