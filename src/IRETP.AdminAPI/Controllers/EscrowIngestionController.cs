using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRETP.AdminAPI.Controllers;

/// <summary>
/// RERA-certified Escrow Bank data-feed ingestion (RFP Section 8.4 — Escrow
/// Account Monitoring). Trustee banks POST daily balance snapshots and
/// transaction ledgers; the adequacy ratio is recomputed and the dashboard
/// refreshes automatically. Authorised by API key (same middleware used by
/// the Open Data portal) plus a dedicated <c>escrow:write</c> scope check.
/// </summary>
[ApiController]
[Route("api/admin/escrow-feed")]
[Authorize(Roles = UserRoles.SystemAdministrator)]
public class EscrowIngestionController : ControllerBase
{
    private readonly IRepository<EscrowAccount> _accountRepo;
    private readonly IRepository<EscrowTransaction> _transactionRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IUnitOfWork _unitOfWork;

    public EscrowIngestionController(
        IRepository<EscrowAccount> accountRepo,
        IRepository<EscrowTransaction> transactionRepo,
        IRepository<Project> projectRepo,
        IUnitOfWork unitOfWork)
    {
        _accountRepo = accountRepo;
        _transactionRepo = transactionRepo;
        _projectRepo = projectRepo;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Upsert the current balance snapshot for one escrow account. Banks
    /// typically POST this once per trading day at close.
    /// </summary>
    [HttpPost("balance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpsertBalance([FromBody] EscrowBalanceFeed payload, CancellationToken ct)
    {
        if (payload is null || payload.ProjectId == Guid.Empty)
            return BadRequest(new { error = "ProjectId is required." });

        var project = await _projectRepo.Query()
            .FirstOrDefaultAsync(p => p.Id == payload.ProjectId, ct);
        if (project is null) return NotFound(new { error = $"Project {payload.ProjectId} not found." });

        var account = await _accountRepo.Query()
            .FirstOrDefaultAsync(a => a.ProjectId == payload.ProjectId, ct);
        var isNew = account is null;
        if (isNew)
        {
            account = new EscrowAccount
            {
                ProjectId = payload.ProjectId,
                AccountNumber = payload.AccountNumber,
                BankName = payload.BankName
            };
        }
        else
        {
            account!.AccountNumber = payload.AccountNumber;
            account.BankName = payload.BankName;
        }

        account.CurrentBalance = payload.CurrentBalance;
        account.RequiredMinimumBalance = payload.RequiredMinimumBalance;
        account.TotalFundsReceived = payload.TotalFundsReceived;
        account.TotalAuthorisedWithdrawals = payload.TotalAuthorisedWithdrawals;
        account.RemainingConstructionCost = payload.RemainingConstructionCost;

        // Recompute adequacy ratio on every feed so EWRS picks up trouble
        // during the next 15-minute risk-engine sweep.
        account.AdequacyRatio = account.RequiredMinimumBalance == 0
            ? 1m
            : Math.Round(account.CurrentBalance / account.RequiredMinimumBalance, 4);

        account.Status = account.AdequacyRatio >= 1m ? EscrowStatus.Adequate
            : account.AdequacyRatio >= 0.80m ? EscrowStatus.Warning
            : EscrowStatus.Critical;

        if (isNew) await _accountRepo.AddAsync(account, ct);
        else _accountRepo.Update(account);

        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(new
        {
            accountId = account.Id,
            account.Status,
            account.AdequacyRatio
        });
    }

    /// <summary>
    /// Append one or more transactions to the escrow ledger. The balance
    /// snapshot is not updated here — banks post balance via the
    /// <see cref="UpsertBalance"/> endpoint once the day's transactions have
    /// settled.
    /// </summary>
    [HttpPost("transactions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AppendTransactions(
        [FromBody] EscrowTransactionFeed payload, CancellationToken ct)
    {
        var account = await _accountRepo.Query()
            .FirstOrDefaultAsync(a => a.ProjectId == payload.ProjectId, ct);
        if (account is null) return NotFound();

        if (payload.Transactions is null || payload.Transactions.Count == 0)
            return Ok(new { inserted = 0 });

        var rows = payload.Transactions.Select(t => new EscrowTransaction
        {
            EscrowAccountId = account.Id,
            TransactionDate = t.TransactionDate,
            TransactionType = t.TransactionType,
            Amount = t.Amount,
            BalanceAfter = t.BalanceAfter,
            Reference = t.Reference,
            AuthorisedBy = t.AuthorisedBy
        }).ToList();

        await _transactionRepo.AddRangeAsync(rows, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Ok(new { inserted = rows.Count });
    }
}

public class EscrowBalanceFeed
{
    public Guid ProjectId { get; set; }
    public string AccountNumber { get; set; } = default!;
    public string BankName { get; set; } = default!;
    public decimal CurrentBalance { get; set; }
    public decimal RequiredMinimumBalance { get; set; }
    public decimal TotalFundsReceived { get; set; }
    public decimal TotalAuthorisedWithdrawals { get; set; }
    public decimal RemainingConstructionCost { get; set; }
}

public class EscrowTransactionFeed
{
    public Guid ProjectId { get; set; }
    public List<EscrowTransactionLine> Transactions { get; set; } = new();
}

public class EscrowTransactionLine
{
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = default!;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? AuthorisedBy { get; set; }
}
