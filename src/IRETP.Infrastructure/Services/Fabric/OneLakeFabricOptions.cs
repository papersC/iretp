using IRETP.Application.DTOs.Fabric;

namespace IRETP.Infrastructure.Services.Fabric;

/// <summary>
/// Bound to the "Fabric" section of appsettings. Controls how the IRETP runtime
/// connects to DLD's Microsoft Fabric / OneLake environment (RFP v1.3 §11.4).
/// </summary>
public sealed class OneLakeFabricOptions
{
    public const string SectionName = "Fabric";

    public FabricSourceMode Mode { get; set; } = FabricSourceMode.Sql;

    /// <summary>Microsoft Fabric workspace identifier (UUID).</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Lakehouse identifier (UUID) within the workspace that contains the Silver/Gold layers.</summary>
    public string? LakehouseId { get; set; }

    /// <summary>Fully qualified OneLake DFS endpoint (e.g. https://onelake.dfs.fabric.microsoft.com).</summary>
    public string? OneLakeEndpoint { get; set; } = "https://onelake.dfs.fabric.microsoft.com";

    /// <summary>Path under the lakehouse where the analytics-ready Gold tables live.</summary>
    public string? GoldLayerPath { get; set; } = "Tables/Gold";

    /// <summary>Path under the lakehouse where the conformed Silver tables live (for audit / lineage).</summary>
    public string? SilverLayerPath { get; set; } = "Tables/Silver";

    /// <summary>Published semantic-model name (XMLA) used by DAX queries when Mode = FabricSemanticModel.</summary>
    public string? SemanticModelName { get; set; }

    /// <summary>Region the Fabric workspace is hosted in. Must be a UAE region in production
    /// (RFP §10.2 — data residency).</summary>
    public string? Region { get; set; } = "UAE North";

    /// <summary>Optional override for the Data Factory pipeline status feed URL.</summary>
    public string? DataFactoryStatusUrl { get; set; }
}
