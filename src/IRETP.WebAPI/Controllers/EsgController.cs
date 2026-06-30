using IRETP.Application.DTOs;
using IRETP.Application.Features.Esg.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EsgController : ControllerBase
{
    private readonly IMediator _mediator;

    public EsgController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Public ESG / Sustainability dashboard (RFP Section 20).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(EsgDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEsgDashboardQuery(), ct);
        return Ok(result);
    }
}
