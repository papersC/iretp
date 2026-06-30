using IRETP.Application.Interfaces;
using IRETP.Infrastructure.Services;

namespace IRETP.Tests.Infrastructure;

public class TimeSeriesAnalyzerTests
{
    private readonly ITimeSeriesAnalyzer _analyzer = new TimeSeriesAnalyzer();

    private static List<TimeSeriesPoint> MonthlySeries(params double[] values) =>
        values
            .Select((v, i) => new TimeSeriesPoint(new DateTime(2025, 1, 1).AddMonths(i), v))
            .ToList();

    // ----- DetectAnomalies --------------------------------------------------

    [Fact]
    public void DetectAnomalies_returns_empty_for_short_series()
    {
        var series = MonthlySeries(10, 11, 12); // = warmUpWindow
        Assert.Empty(_analyzer.DetectAnomalies(series));
    }

    [Fact]
    public void DetectAnomalies_returns_empty_for_constant_series()
    {
        var series = MonthlySeries(100, 100, 100, 100, 100, 100);
        Assert.Empty(_analyzer.DetectAnomalies(series));
    }

    [Fact]
    public void DetectAnomalies_flags_a_clear_spike()
    {
        // 11 small values + one huge spike at month 12.
        var series = MonthlySeries(100, 102, 99, 101, 100, 103, 98, 100, 101, 99, 102, 500);

        var anomalies = _analyzer.DetectAnomalies(series, zThreshold: 2.0);

        var spike = Assert.Single(anomalies);
        Assert.Equal(500, spike.Value);
        Assert.Equal(AnomalyDirection.Spike, spike.Direction);
        Assert.True(spike.ZScore > 2.0);
    }

    [Fact]
    public void DetectAnomalies_flags_a_clear_drop_with_negative_z()
    {
        var series = MonthlySeries(100, 102, 99, 101, 100, 103, 98, 100, 101, 99, 102, 5);

        var anomalies = _analyzer.DetectAnomalies(series, zThreshold: 2.0);

        var drop = Assert.Single(anomalies);
        Assert.Equal(5, drop.Value);
        Assert.Equal(AnomalyDirection.Drop, drop.Direction);
        Assert.True(drop.ZScore < -2.0);
    }

    [Fact]
    public void DetectAnomalies_excludes_warmup_period()
    {
        // First value is huge but inside the warm-up window so must be ignored.
        var series = MonthlySeries(9999, 100, 102, 99, 101, 100, 103);
        Assert.Empty(_analyzer.DetectAnomalies(series, zThreshold: 1.5));
    }

    // ----- AnalyzeTrend -----------------------------------------------------

    [Fact]
    public void AnalyzeTrend_detects_clear_increasing_line()
    {
        var series = MonthlySeries(100, 110, 120, 130, 140, 150);
        var summary = _analyzer.AnalyzeTrend(series);

        Assert.Equal(TrendDirection.Increasing, summary.Direction);
        Assert.True(summary.Slope > 0);
        Assert.True(summary.RSquared > 0.99); // perfect linear fit
    }

    [Fact]
    public void AnalyzeTrend_detects_clear_decreasing_line()
    {
        var series = MonthlySeries(150, 140, 130, 120, 110, 100);
        var summary = _analyzer.AnalyzeTrend(series);

        Assert.Equal(TrendDirection.Decreasing, summary.Direction);
        Assert.True(summary.Slope < 0);
    }

    [Fact]
    public void AnalyzeTrend_treats_tiny_slope_as_flat()
    {
        // 0.1% drift per period — well below the 0.5% threshold.
        var series = MonthlySeries(1000, 1001, 1000, 1001, 1000, 1001);
        var summary = _analyzer.AnalyzeTrend(series);

        Assert.Equal(TrendDirection.Flat, summary.Direction);
    }

    [Fact]
    public void AnalyzeTrend_handles_single_point_safely()
    {
        var summary = _analyzer.AnalyzeTrend(MonthlySeries(42));
        Assert.Equal(TrendDirection.Flat, summary.Direction);
        Assert.Equal(0, summary.Slope);
    }

    // ----- Correlate --------------------------------------------------------

    [Fact]
    public void Correlate_returns_one_for_perfect_positive_relationship()
    {
        var x = new double[] { 1, 2, 3, 4, 5 };
        var y = new double[] { 10, 20, 30, 40, 50 };

        Assert.Equal(1.0, _analyzer.Correlate(x, y));
    }

    [Fact]
    public void Correlate_returns_minus_one_for_perfect_inverse_relationship()
    {
        var x = new double[] { 1, 2, 3, 4, 5 };
        var y = new double[] { 50, 40, 30, 20, 10 };

        Assert.Equal(-1.0, _analyzer.Correlate(x, y));
    }

    [Fact]
    public void Correlate_returns_null_when_either_series_has_no_variance()
    {
        var x = new double[] { 1, 2, 3, 4, 5 };
        var y = new double[] { 7, 7, 7, 7, 7 };

        Assert.Null(_analyzer.Correlate(x, y));
    }

    [Fact]
    public void Correlate_returns_null_when_series_too_short()
    {
        Assert.Null(_analyzer.Correlate(new double[] { 1, 2 }, new double[] { 3, 4 }));
    }

    [Fact]
    public void Correlate_throws_for_unequal_length()
    {
        Assert.Throws<ArgumentException>(() =>
            _analyzer.Correlate(new double[] { 1, 2, 3 }, new double[] { 1, 2 }));
    }
}
