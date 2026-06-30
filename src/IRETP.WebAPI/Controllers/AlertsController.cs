using System.Security.Claims;
using IRETP.Application.Features.Alerts.Commands;
using IRETP.Application.Features.Alerts.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AlertsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException();

    /// <summary>
    /// Get paginated notifications for the current user.
    /// </summary>
    [HttpGet("notifications")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = new GetUserNotificationsQuery
        {
            UserId = GetUserId(),
            IsRead = isRead,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get the current user's alert configurations.
    /// </summary>
    [HttpGet("configurations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfigurations(CancellationToken ct = default)
    {
        var query = new GetUserAlertsQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Configure a new alert for the current user.
    /// </summary>
    [HttpPost("configure")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfigureAlert(
        [FromBody] ConfigureInvestorAlertCommand command,
        CancellationToken ct = default)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command, ct);
        return Ok(new { id });
    }

    /// <summary>
    /// Delete an alert configuration by id.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAlert(Guid id, CancellationToken ct = default)
    {
        var command = new DeleteInvestorAlertCommand
        {
            Id = id,
            UserId = GetUserId()
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok() : NotFound();
    }

    /// <summary>
    /// Mark a single notification as read.
    /// </summary>
    [HttpPut("notifications/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNotificationRead(Guid id, CancellationToken ct = default)
    {
        var command = new MarkNotificationReadCommand
        {
            Id = id,
            UserId = GetUserId()
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok() : NotFound();
    }

    /// <summary>
    /// Mark all notifications as read for the current user.
    /// </summary>
    [HttpPut("notifications/read-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllNotificationsRead(CancellationToken ct = default)
    {
        var command = new MarkAllNotificationsReadCommand { UserId = GetUserId() };
        var count = await _mediator.Send(command, ct);
        return Ok(new { markedRead = count });
    }

    /// <summary>
    /// Get the current user's watchlist items.
    /// </summary>
    [HttpGet("watchlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWatchlist(CancellationToken ct = default)
    {
        var query = new GetWatchlistQuery { UserId = GetUserId() };
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Add an item to the current user's watchlist.
    /// </summary>
    [HttpPost("watchlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AddWatchlistItem(
        [FromBody] AddWatchlistItemCommand command,
        CancellationToken ct = default)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command, ct);
        return Ok(new { id });
    }

    /// <summary>
    /// Remove an item from the current user's watchlist.
    /// </summary>
    [HttpDelete("watchlist/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveWatchlistItem(Guid id, CancellationToken ct = default)
    {
        var command = new RemoveWatchlistItemCommand
        {
            Id = id,
            UserId = GetUserId()
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok() : NotFound();
    }
}
