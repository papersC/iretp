using System.Security.Claims;
using IRETP.Application.DTOs;
using IRETP.Application.Features.Analytics.Commands;
using IRETP.Application.Features.Analytics.Queries;
using IRETP.Application.Features.Export.Commands;
using IRETP.WebAPI.Middleware;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Execute a slice-and-dice analytics query.
    /// </summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(AnalyticsResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Query(
        [FromBody] AnalyticsQueryRequest request,
        CancellationToken ct = default)
    {
        var query = new ExecuteAnalyticsQuery { Request = request };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Export a slice-and-dice analytics result in the requested format.
    /// Format values: excel | csv | pdf | json (RFP AN004). Anonymous users
    /// must attach a CAPTCHA token (RFP 10.3).
    /// </summary>
    [HttpPost("export/{format}")]
    [CaptchaRequired]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        [FromRoute] string format,
        [FromBody] AnalyticsQueryRequest request,
        CancellationToken ct = default)
    {
        var command = new ExportAnalyticsCommand { Format = format, Request = request };
        var result = await _mediator.Send(command, ct);
        return File(result.FileContent, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Get the current user's saved analytics views.
    /// </summary>
    [Authorize]
    [HttpGet("saved-views")]
    [ProducesResponseType(typeof(List<SavedAnalyticsViewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSavedViews(CancellationToken ct = default)
    {
        var query = new GetSavedViewsQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Save a new analytics view for the current user.
    /// </summary>
    [Authorize]
    [HttpPost("saved-views")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSavedView(
        [FromBody] SaveAnalyticsViewCommand command,
        CancellationToken ct = default)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetSavedViews), new { id });
    }

    /// <summary>
    /// Delete a saved analytics view by id.
    /// </summary>
    [Authorize]
    [HttpDelete("saved-views/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSavedView(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteSavedViewCommand
        {
            Id = id,
            UserId = GetUserId()
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok() : NotFound();
    }

    /// <summary>
    /// Get a shared analytics view by its share token (public, no auth needed).
    /// </summary>
    [HttpGet("shared/{shareToken}")]
    [ProducesResponseType(typeof(SavedAnalyticsViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSharedView(string shareToken, CancellationToken ct = default)
    {
        var query = new GetSharedViewQuery { ShareToken = shareToken };
        var result = await _mediator.Send(query, ct);
        return result is not null ? Ok(result) : NotFound();
    }
}
