using IRETP.Application.Features.DeveloperRating.Queries;
using IRETP.Application.Features.Ownership.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/developers")]
public class DeveloperController : ControllerBase
{
    private readonly IMediator _mediator;

    public DeveloperController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get public scorecards for all active developers.
    /// </summary>
    [HttpGet("scorecards")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScorecards(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPublicDeveloperScorecardsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get public scorecard for a single developer.
    /// </summary>
    [HttpGet("{id:guid}/scorecard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeveloperScorecard(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPublicDeveloperScorecardsQuery(), ct);
        var scorecard = result.FirstOrDefault(s => s.Id == id);

        if (scorecard == null)
            return NotFound();

        return Ok(scorecard);
    }

    /// <summary>
    /// Publicly disclosable beneficial ownership structure for a developer
    /// (RFP Section 20). Returns an empty list when no disclosure has been
    /// made — that itself is a transparency signal.
    /// </summary>
    [HttpGet("{id:guid}/ownership")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeneficialOwnership(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBeneficialOwnershipQuery { DeveloperId = id }, ct);
        return Ok(result);
    }
}
