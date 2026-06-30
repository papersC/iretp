using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Map.Queries;

/// <summary>
/// Dedicated Zone Comparison module (RFP AN-005). Returns the same
/// <see cref="ZoneDetailDto"/> shape as the map click-through but for up to
/// five zones in a single call, so the external portal can render a
/// side-by-side comparison table + overlaid charts without N round-trips.
/// </summary>
public class GetZoneComparisonQuery : IRequest<List<ZoneDetailDto>>
{
    /// <summary>RFP AN-005 caps comparison at 5 zones.</summary>
    public const int MaxZonesPerComparison = 5;

    public List<Guid> ZoneIds { get; set; } = [];
}
