using IRETP.Infrastructure.Services;
using IRETP.WebAPI.Middleware;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/investment-profile")]
public class InvestmentProfileController : ControllerBase
{
    private readonly InvestorScorecardPdfService _pdfService;

    public InvestmentProfileController(InvestorScorecardPdfService pdfService)
    {
        _pdfService = pdfService;
    }

    /// <summary>
    /// Zone-level PDF Investment Profile (RFP Section 20 — Phase 4 deliverable
    /// #52). Anonymous users must solve the CAPTCHA on first download per
    /// Section 10.3.
    /// </summary>
    [HttpGet("zone/{zoneId:guid}")]
    [CaptchaRequired]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetZoneReport(Guid zoneId, CancellationToken ct = default)
    {
        try
        {
            var pdf = await _pdfService.RenderZoneAsync(zoneId, ct);
            return File(pdf, "application/pdf", $"IRETP_ZoneProfile_{zoneId:N}.pdf");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Project-level PDF Investment Profile.
    /// </summary>
    [HttpGet("project/{projectId:guid}")]
    [CaptchaRequired]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectReport(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            var pdf = await _pdfService.RenderProjectAsync(projectId, ct);
            return File(pdf, "application/pdf", $"IRETP_ProjectProfile_{projectId:N}.pdf");
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
