using IRETP.Application.Features.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get key performance indicators for the public dashboard.
    /// </summary>
    [HttpGet("kpis")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetKpis(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDashboardKpisQuery(), ct);
        return Ok(result);
    }
}
