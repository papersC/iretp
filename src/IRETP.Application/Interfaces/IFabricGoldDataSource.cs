using IRETP.Application.DTOs.Fabric;

namespace IRETP.Application.Interfaces;

/// <summary>
/// Abstraction for reading analytics-ready data from DLD's Microsoft Fabric / OneLake
/// Gold layer (RFP v1.3 §11.4). All public-portal aggregates and KPIs should be
/// servable through this interface so the active data source is configurable
/// without touching application code.
/// </summary>
public interface IFabricGoldDataSource
{
    /// <summary>The configured mode (Passthrough, OneLakeDirect, FabricSemanticModel, etc.).</summary>
    FabricSourceMode Mode { get; }

    /// <summary>Health probe — must succeed before the platform serves Gold-sourced data.</summary>
    Task<FabricHealthDto> ProbeAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the published semantic model catalog so administrators can see
    /// which Gold tables/measures the platform is configured to consume.</summary>
    Task<IReadOnlyList<FabricSemanticModelDto>> GetSemanticModelsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the most recent freshness watermark recorded by the
    /// configured Data Factory pipeline (RFP §10.1 — Data Freshness).</summary>
    Task<FabricFreshnessDto> GetFreshnessAsync(CancellationToken cancellationToken = default);
}
