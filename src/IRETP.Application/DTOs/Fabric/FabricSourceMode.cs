namespace IRETP.Application.DTOs.Fabric;

/// <summary>
/// How IRETP currently sources analytics-ready data.
///
/// RFP v1.3 §11.4 strongly recommends consuming the existing DLD OneLake Gold
/// layer. The application keeps the OLTP path (Sql) available for the
/// reference build and lower environments where Fabric is not provisioned.
/// </summary>
public enum FabricSourceMode
{
    /// <summary>Reads come from the local OLTP database directly (reference build / lower environments).</summary>
    Sql = 0,

    /// <summary>Reads come from the OneLake Gold layer via Fabric SQL-endpoint or DirectLake (production).</summary>
    OneLakeDirect = 1,

    /// <summary>Reads come from a published Fabric semantic model (XMLA / DAX endpoint).</summary>
    FabricSemanticModel = 2,

    /// <summary>Passthrough mirror over the local OLTP store — surfaces the Fabric API contract for
    /// tests and demos without requiring a real Fabric workspace. Watermarks are computed locally.</summary>
    PassthroughMirror = 3
}
