using IRETP.Application.Features.Audit.Queries;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/audit")]
[Authorize(Roles = $"{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
public class AuditController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuditController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Browse audit log entries. Supports filtering by entity type, action,
    /// user, date range, and a free-text search across entity id and payloads.
    /// Restricted to DLD Supervisor and System Administrator roles per RFP 10.3.
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] string? action,
        [FromQuery] string? userId,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = new GetAuditLogsQuery
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            Search = search,
            From = from,
            To = to,
            Page = page,
            PageSize = Math.Clamp(pageSize, 1, 200)
        };

        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }
}
