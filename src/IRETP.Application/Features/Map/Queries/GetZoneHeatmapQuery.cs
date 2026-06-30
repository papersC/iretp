using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Map.Queries;

public class GetZoneHeatmapQuery : IRequest<List<ZoneHeatmapDto>>;

public class ZoneHeatmapDto
{
    public Guid ZoneId { get; set; }
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public double? CenterLat { get; set; }
    public double? CenterLng { get; set; }
    public string? GeoJson { get; set; }
    public int TransactionCount { get; set; }
    public decimal AvgPricePerSqft { get; set; }
    public decimal AvgRentalYield { get; set; }
}
