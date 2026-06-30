using IRETP.Application.DTOs;
using IRETP.Application.Features.EWRS.Commands;
using IRETP.Application.Features.EWRS.Queries;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/ewrs")]
[Authorize(Roles = $"{UserRoles.DldViewer},{UserRoles.DldOperator},{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
public class EwrsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EwrsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get the Early Warning and Risk System dashboard summary.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEwrsDashboardQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get paginated risk alerts.
    /// </summary>
    [HttpGet("alerts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] AlertStatus? status,
        [FromQuery] RiskLevel? riskLevel,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var query = new GetRiskAlertsQuery
        {
            Status = status,
            RiskLevel = riskLevel,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Acknowledge a risk alert.
    /// </summary>
    [HttpPut("alerts/{id:guid}/acknowledge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcknowledgeAlert(Guid id, CancellationToken ct = default)
    {
        var command = new AcknowledgeAlertCommand
        {
            AlertId = id,
            UserId = User.Identity?.Name ?? "system"
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok(new { message = "Alert acknowledged", alertId = id }) : NotFound();
    }

    /// <summary>
    /// Resolve a risk alert.
    /// </summary>
    [HttpPut("alerts/{id:guid}/resolve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveAlert(
        Guid id, [FromBody] ResolveAlertRequest request, CancellationToken ct = default)
    {
        var command = new ResolveAlertCommand
        {
            AlertId = id,
            UserId = User.Identity?.Name ?? "system",
            ActionNotes = request.ActionNotes
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok(new { message = "Alert resolved", alertId = id }) : NotFound();
    }

    /// <summary>
    /// Get all risk thresholds.
    /// </summary>
    [HttpGet("thresholds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetThresholds(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetRiskThresholdsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Update a risk threshold (Supervisor / Admin only).
    /// </summary>
    [HttpPut("thresholds/{id:guid}")]
    [Authorize(Roles = $"{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateThreshold(
        Guid id, [FromBody] UpdateThresholdRequest request, CancellationToken ct = default)
    {
        var command = new UpdateRiskThresholdCommand
        {
            ThresholdId = id,
            ThresholdValue = request.ThresholdValue,
            DefaultRiskLevel = request.DefaultRiskLevel,
            DefaultAlertLevel = request.DefaultAlertLevel,
            UserId = User.Identity?.Name ?? "system"
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok(new { message = "Threshold updated", thresholdId = id }) : NotFound();
    }

    // -----------------------------------------------------------------------
    // Playbook (RFP Section 8.3)
    // -----------------------------------------------------------------------

    [HttpGet("playbooks")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlaybooks(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetPlaybooksQuery(), ct);
        return Ok(result);
    }

    [HttpPut("playbooks/{thresholdId:guid}")]
    [Authorize(Roles = $"{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePlaybook(
        Guid thresholdId, [FromBody] UpdatePlaybookRequest request, CancellationToken ct = default)
    {
        var command = new UpdatePlaybookCommand
        {
            ThresholdId = thresholdId,
            Steps = request.Steps,
            ModifiedBy = User.Identity?.Name ?? "system"
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok(new { message = "Playbook updated" }) : NotFound();
    }

    [HttpPut("alerts/{alertId:guid}/playbook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAlertPlaybook(
        Guid alertId, [FromBody] UpdateAlertPlaybookRequest request, CancellationToken ct = default)
    {
        var command = new UpdateAlertPlaybookProgressCommand
        {
            AlertId = alertId,
            Progress = request.Progress,
            UpdatedBy = User.Identity?.Name ?? "system"
        };

        var result = await _mediator.Send(command, ct);
        return result ? Ok(new { message = "Playbook progress updated" }) : NotFound();
    }
}

// Request DTOs for controller binding

public class ResolveAlertRequest
{
    public string? ActionNotes { get; set; }
}

public class UpdatePlaybookRequest
{
    public List<string> Steps { get; set; } = [];
}

public class UpdateAlertPlaybookRequest
{
    public List<PlaybookProgressEntry> Progress { get; set; } = [];
}

public class UpdateThresholdRequest
{
    public decimal ThresholdValue { get; set; }
    public RiskLevel DefaultRiskLevel { get; set; }
    public AlertLevel DefaultAlertLevel { get; set; }
}
