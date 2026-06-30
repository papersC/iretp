namespace IRETP.Application.DTOs.Fabric;

public sealed record FabricHealthDto(
    FabricSourceMode Mode,
    bool Available,
    string? WorkspaceId,
    string? LakehouseId,
    string? Region,
    string? Detail,
    DateTime ProbedAtUtc);
