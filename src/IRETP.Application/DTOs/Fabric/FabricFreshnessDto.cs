namespace IRETP.Application.DTOs.Fabric;

public sealed record FabricFreshnessDto(
    DateTime? GoldLayerLastWriteUtc,
    DateTime? SilverLayerLastWriteUtc,
    TimeSpan? TransactionLag,
    TimeSpan? KpiLag,
    string? LastPipelineRunId,
    string? LastPipelineStatus);
