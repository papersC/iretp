using IRETP.Application.Features.Map.Queries;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MapController : ControllerBase
{
    private readonly IMediator _mediator;

    public MapController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get zone heatmap data for the interactive map.
    /// </summary>
    [HttpGet("zones/heatmap")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZoneHeatmap(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetZoneHeatmapQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get details for a specific zone.
    /// </summary>
    [HttpGet("zones/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZone(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetZoneDetailQuery { ZoneId = id }, ct);
        return result != null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// Side-by-side comparison of up to 5 zones (RFP AN-005). Pass each
    /// zone id as a repeated query parameter, e.g.
    /// <c>?zoneIds=...&amp;zoneIds=...</c>. Returns the same per-zone shape
    /// as the single-zone detail endpoint so the frontend can render a
    /// consistent comparison grid.
    /// </summary>
    [HttpGet("zones/compare")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompareZones(
        [FromQuery(Name = "zoneIds")] List<Guid> zoneIds,
        CancellationToken ct)
    {
        if (zoneIds.Count == 0)
            return BadRequest(new { message = "At least one zoneId is required." });
        if (zoneIds.Count > GetZoneComparisonQuery.MaxZonesPerComparison)
            return BadRequest(new
            {
                message = $"Comparison capped at {GetZoneComparisonQuery.MaxZonesPerComparison} zones (RFP AN-005)."
            });

        var result = await _mediator.Send(new GetZoneComparisonQuery { ZoneIds = zoneIds }, ct);
        return Ok(result);
    }

    /// <summary>
    /// List projects visible on the map with optional filters.
    /// </summary>
    [HttpGet("projects")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjects(
        [FromQuery] Guid? zoneId,
        [FromQuery] ProjectStatus? status,
        [FromQuery] Guid? developerId,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProjectsMapQuery
        {
            ZoneId = zoneId,
            Status = status,
            DeveloperId = developerId
        }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get details for a specific project.
    /// </summary>
    [HttpGet("projects/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProject(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProjectDetailQuery { ProjectId = id }, ct);
        return result != null ? Ok(result) : NotFound();
    }
}
