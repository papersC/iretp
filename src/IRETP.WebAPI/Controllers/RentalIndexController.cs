using IRETP.Application.Features.RentalIndex.Queries;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/rental-index")]
public class RentalIndexController : ControllerBase
{
    private readonly IMediator _mediator;

    public RentalIndexController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get the rental index trend data with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRentalIndex(
        [FromQuery] Guid? zoneId,
        [FromQuery] PropertyType? unitType,
        [FromQuery] bool? isShortTerm,
        [FromQuery] int? yearFrom,
        [FromQuery] int? yearTo,
        CancellationToken ct)
    {
        var query = new GetRentalIndexQuery
        {
            ZoneId = zoneId,
            UnitType = unitType,
            IsShortTerm = isShortTerm,
            YearFrom = yearFrom,
            YearTo = yearTo
        };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Calculate rental yield for given zones and unit type.
    /// Gross Yield = (Annual Rent / Transaction Price) x 100
    /// </summary>
    [HttpGet("yield-calculator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> YieldCalculator(
        [FromQuery] List<Guid>? zoneIds,
        [FromQuery] PropertyType? unitType,
        CancellationToken ct)
    {
        var query = new GetRentalYieldCalculatorQuery
        {
            ZoneIds = zoneIds,
            UnitType = unitType
        };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
