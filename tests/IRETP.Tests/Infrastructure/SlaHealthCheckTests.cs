using IRETP.Application.DTOs;
using IRETP.Application.DTOs.Fabric;
using IRETP.Application.Interfaces;
using IRETP.Infrastructure.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IRETP.Tests.Infrastructure;

/// <summary>
/// Covers the aggregated platform SLA probe (§10.1) and especially the v1.3
/// addition: Microsoft Fabric / OneLake Gold freshness must surface in the
/// same health check as KPI and AI latency.
/// </summary>
public class SlaHealthCheckTests
{
    [Fact]
    public async Task Healthy_when_everything_within_budget()
    {
        var sut = new SlaHealthCheck(
            new FakeAiMetrics(),
            new FakeKpiCache(DateTime.UtcNow.AddMinutes(-1)),
            new FakeFabric(FabricSourceMode.PassthroughMirror, TimeSpan.FromMinutes(30), "Succeeded"));

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("§10.1 metrics within budget", result.Description);
    }

    [Fact]
    public async Task Degraded_when_fabric_transaction_lag_exceeds_24h_budget()
    {
        var sut = new SlaHealthCheck(
            new FakeAiMetrics(),
            new FakeKpiCache(DateTime.UtcNow.AddMinutes(-1)),
            new FakeFabric(FabricSourceMode.OneLakeDirect, TimeSpan.FromHours(30), "Succeeded"));

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("24h §10.1 budget", result.Description);
    }

    [Fact]
    public async Task Unhealthy_when_fabric_transaction_lag_exceeds_48h_hard_ceiling()
    {
        var sut = new SlaHealthCheck(
            new FakeAiMetrics(),
            new FakeKpiCache(DateTime.UtcNow.AddMinutes(-1)),
            new FakeFabric(FabricSourceMode.OneLakeDirect, TimeSpan.FromHours(72), "Succeeded"));

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("48h hard ceiling", result.Description);
    }

    [Fact]
    public async Task Unhealthy_when_fabric_data_factory_pipeline_status_is_Failed()
    {
        var sut = new SlaHealthCheck(
            new FakeAiMetrics(),
            new FakeKpiCache(DateTime.UtcNow.AddMinutes(-1)),
            new FakeFabric(FabricSourceMode.OneLakeDirect, TimeSpan.FromMinutes(5), "Failed"));

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Data Factory pipeline status reports Failed", result.Description);
    }

    [Fact]
    public async Task Probe_failure_adds_warning_without_breaking_health_check()
    {
        var sut = new SlaHealthCheck(
            new FakeAiMetrics(),
            new FakeKpiCache(DateTime.UtcNow.AddMinutes(-1)),
            new ThrowingFabric());

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("Fabric freshness probe failed", result.Description);
    }

    [Fact]
    public async Task Cancellation_is_propagated_not_swallowed_as_degraded()
    {
        // If the caller cancels the health-check, we should honour that cancellation
        // instead of dressing it up as a "probe failed" warning.
        var sut = new SlaHealthCheck(
            new FakeAiMetrics(),
            new FakeKpiCache(DateTime.UtcNow.AddMinutes(-1)),
            new CancellingFabric());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.CheckHealthAsync(new HealthCheckContext(), cts.Token));
    }

    [Fact]
    public async Task Fabric_metadata_surfaced_in_health_check_data_bag()
    {
        var watermark = DateTime.UtcNow.AddMinutes(-10);
        var sut = new SlaHealthCheck(
            new FakeAiMetrics(),
            new FakeKpiCache(DateTime.UtcNow.AddMinutes(-1)),
            new FakeFabric(FabricSourceMode.OneLakeDirect, TimeSpan.FromMinutes(10), "Succeeded", watermark));

        var result = await sut.CheckHealthAsync(new HealthCheckContext());

        Assert.True(result.Data.ContainsKey("fabric.mode"));
        Assert.Equal("OneLakeDirect", result.Data["fabric.mode"]);
        Assert.Equal("Succeeded", result.Data["fabric.pipelineStatus"]);
        Assert.Equal(10, result.Data["fabric.transactionLagMinutes"]);
    }

    // --- Test doubles --------------------------------------------------------

    private sealed class FakeAiMetrics : IAIModelMetrics
    {
        public void RecordSuccess(string modelName, double latencyMs) { }
        public void RecordFailure(string modelName, string? errorMessage) { }
        public void SetActive(string modelName, bool active) { }
        public void SetMetadata(string modelName, string tier, string region) { }
        public IReadOnlyList<AIModelSnapshot> Snapshot() => Array.Empty<AIModelSnapshot>();
    }

    private sealed class FakeKpiCache : IKpiSnapshotCache
    {
        public FakeKpiCache(DateTime refreshedAt)
        {
            Current = new DashboardKpiDto { RefreshedAt = refreshedAt };
        }
        public DashboardKpiDto? Current { get; }
        public void Set(DashboardKpiDto snapshot) { }
    }

    private sealed class FakeFabric : IFabricGoldDataSource
    {
        private readonly TimeSpan _lag;
        private readonly string _pipelineStatus;
        private readonly DateTime _goldWatermark;

        public FabricSourceMode Mode { get; }

        public FakeFabric(FabricSourceMode mode, TimeSpan lag, string pipelineStatus, DateTime? watermark = null)
        {
            Mode = mode;
            _lag = lag;
            _pipelineStatus = pipelineStatus;
            _goldWatermark = watermark ?? DateTime.UtcNow.Subtract(lag);
        }

        public Task<FabricHealthDto> ProbeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new FabricHealthDto(Mode, true, "ws", "lh", "UAE North", null, DateTime.UtcNow));

        public Task<IReadOnlyList<FabricSemanticModelDto>> GetSemanticModelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FabricSemanticModelDto>>(Array.Empty<FabricSemanticModelDto>());

        public Task<FabricFreshnessDto> GetFreshnessAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new FabricFreshnessDto(_goldWatermark, _goldWatermark, _lag, _lag, "run-1", _pipelineStatus));
    }

    private sealed class ThrowingFabric : IFabricGoldDataSource
    {
        public FabricSourceMode Mode => FabricSourceMode.OneLakeDirect;
        public Task<FabricHealthDto> ProbeAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<FabricSemanticModelDto>> GetSemanticModelsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FabricFreshnessDto> GetFreshnessAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("simulated probe failure");
    }

    private sealed class CancellingFabric : IFabricGoldDataSource
    {
        public FabricSourceMode Mode => FabricSourceMode.OneLakeDirect;
        public Task<FabricHealthDto> ProbeAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<FabricSemanticModelDto>> GetSemanticModelsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<FabricFreshnessDto> GetFreshnessAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new FabricFreshnessDto(null, null, null, null, null, null));
        }
    }
}
