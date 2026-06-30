using IRETP.Application.Features.Map.Queries;
using IRETP.Application.Features.PriceIndex.Queries;
using IRETP.Application.Features.RentalIndex.Queries;
using IRETP.Application.Features.Transactions.Queries;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/v1/open-data")]
public class OpenDataController : ControllerBase
{
    private readonly IMediator _mediator;

    public OpenDataController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Public open-data endpoint for transactions.
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] List<Guid>? zoneIds,
        [FromQuery] List<PropertyType>? propertyTypes,
        [FromQuery] List<TransactionType>? transactionTypes,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken ct = default)
    {
        var query = new GetTransactionsQuery
        {
            ZoneIds = zoneIds,
            PropertyTypes = propertyTypes,
            TransactionTypes = transactionTypes,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Page = page,
            PageSize = Math.Min(pageSize, 1000)
        };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Public open-data endpoint for zones.
    /// </summary>
    [HttpGet("zones")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZones(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetZoneHeatmapQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Public open-data endpoint for projects.
    /// </summary>
    [HttpGet("projects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjects(
        [FromQuery] Guid? zoneId,
        [FromQuery] ProjectStatus? status,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProjectsMapQuery
        {
            ZoneId = zoneId,
            Status = status
        }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Public open-data endpoint for the price index.
    /// </summary>
    [HttpGet("price-index")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPriceIndex(
        [FromQuery] Guid? zoneId,
        [FromQuery] PropertyType? propertyType,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPriceIndexQuery
        {
            ZoneId = zoneId,
            PropertyType = propertyType
        }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Public open-data endpoint for the rental index.
    /// </summary>
    [HttpGet("rental-index")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRentalIndex(
        [FromQuery] Guid? zoneId,
        [FromQuery] PropertyType? unitType,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRentalIndexQuery
        {
            ZoneId = zoneId,
            UnitType = unitType
        }, ct);
        return Ok(result);
    }
}
