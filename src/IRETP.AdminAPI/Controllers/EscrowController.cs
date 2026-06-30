using IRETP.Application.Features.Escrow.Queries;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using IRETP.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRETP.AdminAPI.Controllers;

[ApiController]
[Route("api/admin/escrow")]
[Authorize]
public class EscrowController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly EscrowHealthReportService _reportService;
    private readonly IRepository<EscrowAccount> _accountRepo;
    private readonly IRepository<EscrowTransaction> _transactionRepo;

    public EscrowController(
        IMediator mediator,
        EscrowHealthReportService reportService,
        IRepository<EscrowAccount> accountRepo,
        IRepository<EscrowTransaction> transactionRepo)
    {
        _mediator = mediator;
        _reportService = reportService;
        _accountRepo = accountRepo;
        _transactionRepo = transactionRepo;
    }

    /// <summary>
    /// Get the escrow monitoring dashboard overview.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEscrowDashboardQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get escrow details for a specific project.
    /// </summary>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectEscrow(Guid projectId, CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetProjectEscrowDetailQuery { ProjectId = projectId }, ct);

        return result is not null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// Generate the Monthly Escrow Health PDF on-demand for a project
    /// (RFP ESC-003). Covers the month specified by <paramref name="year"/>
    /// and <paramref name="month"/>; defaults to the most recently-closed month.
    /// </summary>
    [HttpGet("{projectId:guid}/monthly-report")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateMonthlyReport(
        Guid projectId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct = default)
    {
        var account = await _accountRepo.Query()
            .Include(e => e.Project).ThenInclude(p => p.Developer)
            .Include(e => e.Project).ThenInclude(p => p.Zone)
            .FirstOrDefaultAsync(e => e.ProjectId == projectId, ct);

        if (account is null) return NotFound();

        var refDate = year.HasValue && month.HasValue
            ? new DateTime(year.Value, month.Value, 1)
            : new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);

        var periodStart = new DateTime(refDate.Year, refDate.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var transactions = await _transactionRepo.Query()
            .Where(t => t.EscrowAccountId == account.Id
                        && t.TransactionDate >= periodStart
                        && t.TransactionDate <= periodEnd)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(ct);

        var pdf = _reportService.RenderReport(account, transactions, periodStart, periodEnd);
        var fileName = $"IRETP_Escrow_{account.Project?.Name ?? "Project"}_{periodStart:yyyyMM}.pdf"
            .Replace(' ', '_');

        return File(pdf, "application/pdf", fileName);
    }

    /// <summary>
    /// Get the audit log for a project's escrow account.
    /// </summary>
    [HttpGet("{projectId:guid}/audit-log")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLog(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetEscrowAuditLogQuery
        {
            ProjectId = projectId,
            Page = page,
            PageSize = pageSize
        }, ct);

        return Ok(result);
    }
}
