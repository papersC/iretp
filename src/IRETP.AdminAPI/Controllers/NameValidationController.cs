using IRETP.Application.Features.NameValidation.Commands;
using IRETP.Application.Features.NameValidation.Queries;
using IRETP.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/name-validation")]
[Authorize(Roles = $"{UserRoles.DldOperator},{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
public class NameValidationController : ControllerBase
{
    private readonly IMediator _mediator;

    public NameValidationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Review queue for Arabic-name validation (RFP FR009 acceptance).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] string? entityType,
        [FromQuery] int? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetNameValidationsQuery
        {
            EntityType = entityType,
            Status = status,
            Search = search,
            Page = page,
            PageSize = pageSize
        }, ct);
        return Ok(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetNameValidationSummaryQuery(), ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewRequest request, CancellationToken ct = default)
    {
        var ok = await _mediator.Send(new ReviewNameValidationCommand
        {
            Id = id,
            Status = request.Status,
            OfficialNameAr = request.OfficialNameAr,
            ReviewNotes = request.ReviewNotes,
            ReviewerId = User.Identity?.Name,
            ReviewerName = User.Identity?.Name ?? "DLD reviewer"
        }, ct);
        return ok ? Ok() : NotFound();
    }
}

public class ReviewRequest
{
    public int Status { get; set; }
    public string? OfficialNameAr { get; set; }
    public string? ReviewNotes { get; set; }
}
