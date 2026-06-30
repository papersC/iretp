using IRETP.Application.Features.PriceIndex.Queries;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/price-index")]
public class PriceIndexController : ControllerBase
{
    private readonly IMediator _mediator;

    public PriceIndexController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get the price index trend data with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceIndex(
        [FromQuery] Guid? zoneId,
        [FromQuery] PropertyType? propertyType,
        [FromQuery] bool? isOffPlan,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        CancellationToken ct)
    {
        var query = new GetPriceIndexQuery
        {
            ZoneId = zoneId,
            PropertyType = propertyType,
            IsOffPlan = isOffPlan,
            YearFrom = yearFrom,
            YearTo = yearTo
        };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Compare price indices across up to 5 zones.
    /// </summary>
    [HttpGet("compare")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ComparePriceIndex(
        [FromQuery] List<Guid> zoneIds,
        [FromQuery] PropertyType? propertyType,
        [FromQuery] bool? isOffPlan,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        CancellationToken ct)
    {
        if (zoneIds.Count > 5)
            return BadRequest(new { message = "Maximum 5 zones can be compared." });

        var query = new GetPriceIndexComparisonQuery
        {
            ZoneIds = zoneIds,
            PropertyType = propertyType,
            IsOffPlan = isOffPlan,
            YearFrom = yearFrom,
            YearTo = yearTo
        };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
