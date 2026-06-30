using IRETP.Application.DTOs;
using IRETP.Application.Features.Greti.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GretiController : ControllerBase
{
    private readonly IMediator _mediator;

    public GretiController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Live GRETI progress dashboard derived from current DLD data (RFP 2.2).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(GretiDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetGretiDashboardQuery(), ct);
        return Ok(result);
    }
}
