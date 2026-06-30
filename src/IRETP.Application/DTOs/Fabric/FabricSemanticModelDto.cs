namespace IRETP.Application.DTOs.Fabric;

public sealed record FabricSemanticModelDto(
    string Name,
    string Layer,
    string Description,
    IReadOnlyList<string> Measures,
    IReadOnlyList<string> Dimensions);
