using IRETP.Application.DTOs;
using IRETP.Application.Features.Mortgage.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MortgageController : ControllerBase
{
    private readonly IMediator _mediator;

    public MortgageController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Mortgage &amp; Debt Market Transparency dashboard (RFP Section 20).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(MortgageDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? lookbackMonths,
        CancellationToken ct = default)
    {
        var query = new GetMortgageDashboardQuery { LookbackMonths = lookbackMonths };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
