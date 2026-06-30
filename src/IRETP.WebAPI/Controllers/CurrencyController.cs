using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRETP.WebAPI.Controllers;

/// <summary>
/// Exposes the latest daily FX snapshot (RFP FR005). Kept on the public API
/// so anonymous portal visitors get a currency-aware UI — the payload is
/// non-sensitive reference data.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CurrencyController : ControllerBase
{
    private readonly IRepository<CurrencyRate> _repo;

    public CurrencyController(IRepository<CurrencyRate> repo)
    {
        _repo = repo;
    }

    [HttpGet("rates")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Latest(CancellationToken ct = default)
    {
        var latestByCode = await _repo.Query()
            .GroupBy(r => r.Code)
            .Select(g => g.OrderByDescending(r => r.AsOfDate).First())
            .ToListAsync(ct);

        return Ok(new
        {
            @base = "AED",
            asOf = latestByCode.Count == 0 ? (DateTime?)null : latestByCode.Max(r => r.AsOfDate),
            rates = latestByCode.ToDictionary(
                r => r.Code,
                r => new { r.UnitsPerAed, r.Source, r.AsOfDate })
        });
    }
}
