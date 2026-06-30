using IRETP.Application.Features.DeveloperRating.Commands;
using IRETP.Application.Features.DeveloperRating.Queries;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/developers")]
[Authorize]
public class DeveloperRatingController : ControllerBase
{
    private readonly IMediator _mediator;

    public DeveloperRatingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get the developer leaderboard with composite scores.
    /// </summary>
    [HttpGet("leaderboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery] int? year,
        [FromQuery] int? quarter,
        [FromQuery] int? top,
        CancellationToken ct = default)
    {
        var query = new GetDeveloperLeaderboardQuery
        {
            Year = year,
            Quarter = quarter,
            Top = top
        };

        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get detailed profile and scoring breakdown for a developer.
    /// </summary>
    [HttpGet("{id:guid}/profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeveloperProfile(Guid id, CancellationToken ct = default)
    {
        var query = new GetDeveloperProfileQuery { DeveloperId = id };
        var result = await _mediator.Send(query, ct);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Compare multiple developers side-by-side (up to 4).
    /// </summary>
    [HttpGet("compare")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompareDevelopers(
        [FromQuery] List<Guid> ids, CancellationToken ct = default)
    {
        var query = new CompareDevelopersQuery { Ids = ids };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get the current scoring weights used for the developer rating algorithm.
    /// </summary>
    [HttpGet("scoring-weights")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScoringWeights(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetScoringWeightsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Update scoring weights (System Administrator only).
    /// </summary>
    [HttpPut("scoring-weights")]
    [Authorize(Roles = UserRoles.SystemAdministrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateScoringWeights(
        [FromBody] UpdateScoringWeightsCommand command, CancellationToken ct = default)
    {
        command.ModifiedBy = User.Identity?.Name ?? "system";

        var result = await _mediator.Send(command, ct);
        return Ok(new { success = result });
    }
}
