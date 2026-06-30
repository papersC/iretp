using System.Security.Claims;
using IRETP.Application.Features.CMS.Commands;
using IRETP.Application.Features.CMS.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CmsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CmsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string GetUserId() =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";

    /// <summary>
    /// Get CMS content for a page/section.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetContent(
        [FromQuery] string? pageKey,
        [FromQuery] string? sectionKey,
        [FromQuery] string locale = "en",
        [FromQuery] bool publishedOnly = true,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCmsContentQuery
        {
            PageKey = pageKey,
            SectionKey = sectionKey,
            Locale = locale,
            PublishedOnly = publishedOnly
        }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Create or update CMS content. Every write inserts a version snapshot
    /// so the FR002 history / rollback requirement is satisfied.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "DldOperator,DldSupervisor,SystemAdministrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateContent(
        [FromBody] UpdateCmsContentCommand command, CancellationToken ct)
    {
        command.UserId = GetUserId();
        var id = await _mediator.Send(command, ct);
        return Ok(new { id });
    }

    /// <summary>
    /// Publish staged CMS content to production (RFP FR002).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "DldOperator,DldSupervisor,SystemAdministrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new PublishCmsContentCommand { Id = id, UserId = GetUserId() }, ct);
        return result ? Ok(new { message = "Published successfully" }) : NotFound();
    }

    /// <summary>
    /// List the full version history for a CMS content row.
    /// </summary>
    [HttpGet("{id:guid}/versions")]
    [Authorize(Roles = "DldOperator,DldSupervisor,SystemAdministrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions(
        Guid id, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetCmsVersionsQuery { CmsContentId = id, Limit = limit }, ct);
        return Ok(result);
    }

    /// <summary>
    /// Roll the CMS content back to a historical version. Creates a new
    /// Draft version; does not mutate history.
    /// </summary>
    [HttpPost("{id:guid}/rollback/{versionId:guid}")]
    [Authorize(Roles = "DldSupervisor,SystemAdministrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rollback(Guid id, Guid versionId, CancellationToken ct)
    {
        var ok = await _mediator.Send(new RollbackCmsContentCommand
        {
            CmsContentId = id,
            VersionId = versionId,
            UserId = GetUserId()
        }, ct);
        return ok ? Ok(new { message = "Rolled back — content is now in Draft state." }) : NotFound();
    }

    /// <summary>
    /// Issue a shareable preview token for a specific version so DLD senior
    /// management can review the draft before it is published (FR002).
    /// </summary>
    [HttpPost("versions/{versionId:guid}/preview-link")]
    [Authorize(Roles = "DldOperator,DldSupervisor,SystemAdministrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreatePreviewLink(
        Guid versionId, [FromQuery] int ttlHours = 48, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new CreatePreviewLinkCommand
        {
            VersionId = versionId,
            TtlHours = ttlHours,
            UserId = GetUserId()
        }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Public preview endpoint used by the Razor preview page. Authenticated
    /// access is not required — holding the signed token is sufficient.
    /// </summary>
    [HttpGet("preview/{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreview(string token, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCmsPreviewQuery { Token = token }, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
