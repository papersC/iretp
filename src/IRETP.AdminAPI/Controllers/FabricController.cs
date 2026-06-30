using IRETP.Application.Interfaces;
using IRETP.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.AdminAPI.Controllers;

/// <summary>
/// Microsoft Fabric / OneLake integration panel (RFP v1.3 §11.4 + §11.4.1).
/// Lets DLD administrators see the currently active data source, the catalogue
/// of Gold-layer semantic models the platform consumes, and freshness against
/// the Data Factory pipeline.
/// </summary>
[ApiController]
[Route("api/admin/fabric")]
[Authorize(Roles = $"{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
public sealed class FabricController : ControllerBase
{
    private readonly IFabricGoldDataSource _fabric;

    public FabricController(IFabricGoldDataSource fabric)
    {
        _fabric = fabric;
    }

    /// <summary>Probe the configured Fabric / OneLake adapter for connectivity and mode.</summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var health = await _fabric.ProbeAsync(ct);
        return Ok(health);
    }

    /// <summary>List the Gold-layer semantic models the platform is configured to read.</summary>
    [HttpGet("semantic-models")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SemanticModels(CancellationToken ct)
    {
        var models = await _fabric.GetSemanticModelsAsync(ct);
        return Ok(models);
    }

    /// <summary>Freshness watermark for the Gold layer + last Data Factory pipeline run.</summary>
    [HttpGet("freshness")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Freshness(CancellationToken ct)
    {
        var freshness = await _fabric.GetFreshnessAsync(ct);
        return Ok(freshness);
    }
}
