using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Map.Queries;

public class GetZoneComparisonQueryHandler
    : IRequestHandler<GetZoneComparisonQuery, List<ZoneDetailDto>>
{
    private readonly IMediator _mediator;

    public GetZoneComparisonQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<List<ZoneDetailDto>> Handle(
        GetZoneComparisonQuery request, CancellationToken cancellationToken)
    {
        if (request.ZoneIds.Count == 0) return [];

        // Defensive cap — also enforced at the controller layer, but the
        // handler is the last line of defence against a hand-crafted request.
        var zoneIds = request.ZoneIds
            .Distinct()
            .Take(GetZoneComparisonQuery.MaxZonesPerComparison)
            .ToList();

        // Fan out per-zone detail fetches in parallel. Each call re-uses the
        // existing GetZoneDetailQueryHandler so every metric is calculated
        // identically to the single-zone panel.
        var fetches = zoneIds.Select(id =>
            _mediator.Send(new GetZoneDetailQuery { ZoneId = id }, cancellationToken));

        var results = await Task.WhenAll(fetches);
        return results.Where(r => r is not null).Cast<ZoneDetailDto>().ToList();
    }
}
