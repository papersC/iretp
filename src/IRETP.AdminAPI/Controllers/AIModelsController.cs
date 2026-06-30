using IRETP.Application.Features.AIAgent.Queries;
using IRETP.Application.Interfaces;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/ai-models")]
[Authorize(Roles = $"{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
public class AIModelsController : ControllerBase
{
    private readonly IAIModelMetrics _metrics;
    private readonly IMediator _mediator;
    private readonly IAiAccuracyHarness _accuracyHarness;

    public AIModelsController(IAIModelMetrics metrics, IMediator mediator, IAiAccuracyHarness accuracyHarness)
    {
        _metrics = metrics;
        _mediator = mediator;
        _accuracyHarness = accuracyHarness;
    }

    /// <summary>
    /// AI Model Performance Transparency panel (RFP Section 5.3). Returns the
    /// current snapshot of configured models, their active status, success /
    /// failure counts, and rolling latency averages.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        return Ok(_metrics.Snapshot());
    }

    /// <summary>
    /// AI query audit log (RFP FR005 service-navigation traceability +
    /// 15.3 incident reporting). Supports topic filter, refusal filter,
    /// date range, and free-text search across queries and answers.
    /// </summary>
    [HttpGet("interactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInteractions(
        [FromQuery] string? topic,
        [FromQuery] string? search,
        [FromQuery] bool? wasRefusal,
        [FromQuery] bool? success,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAiInteractionLogsQuery
        {
            Topic = topic,
            Search = search,
            WasRefusal = wasRefusal,
            Success = success,
            From = from,
            To = to,
            Page = page,
            PageSize = pageSize
        }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Run the standardised accuracy test catalog (RFP AI001 — &gt;= 90%
    /// pass rate on a 100-question DLD data set). Returns a per-question
    /// breakdown and the aggregate pass percentage. SystemAdministrator-only
    /// because the harness drives real model calls and incurs API cost.
    /// </summary>
    [HttpPost("accuracy-test")]
    [Authorize(Roles = UserRoles.SystemAdministrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RunAccuracyTest(
        [FromQuery] string? language,
        CancellationToken ct = default)
    {
        var report = await _accuracyHarness.RunAsync(language, ct);
        return Ok(report);
    }
}
