using IRETP.Application.DTOs;
using IRETP.Application.Features.Benchmark.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BenchmarkController : ControllerBase
{
    private readonly IMediator _mediator;

    public BenchmarkController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// International Market Benchmarking — Dubai vs London, Singapore, New York,
    /// Paris, Hong Kong (RFP Section 20).
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(BenchmarkDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetBenchmarkDashboardQuery(), ct);
        return Ok(result);
    }
}
