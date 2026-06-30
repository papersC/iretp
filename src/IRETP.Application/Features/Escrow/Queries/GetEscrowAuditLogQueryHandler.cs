using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Escrow.Queries;

public class GetEscrowAuditLogQueryHandler
    : IRequestHandler<GetEscrowAuditLogQuery, PagedResult<EscrowTransactionDto>>
{
    private readonly IRepository<EscrowAccount> _escrowRepo;
    private readonly IRepository<EscrowTransaction> _transactionRepo;

    public GetEscrowAuditLogQueryHandler(
        IRepository<EscrowAccount> escrowRepo,
        IRepository<EscrowTransaction> transactionRepo)
    {
        _escrowRepo = escrowRepo;
        _transactionRepo = transactionRepo;
    }

    public async Task<PagedResult<EscrowTransactionDto>> Handle(
        GetEscrowAuditLogQuery request, CancellationToken cancellationToken)
    {
        // Find the escrow account for the project
        var accounts = await _escrowRepo.FindAsync(
            e => e.ProjectId == request.ProjectId, cancellationToken);
        var escrowAccount = accounts.FirstOrDefault();

        if (escrowAccount is null)
            return new PagedResult<EscrowTransactionDto>([], 0, request.Page, request.PageSize);

        var query = _transactionRepo.Query()
            .Where(t => t.EscrowAccountId == escrowAccount.Id)
            .OrderByDescending(t => t.TransactionDate);

        var totalCount = query.Count();

        var items = query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new EscrowTransactionDto
            {
                Id = t.Id,
                EscrowAccountId = t.EscrowAccountId,
                TransactionDate = t.TransactionDate,
                TransactionType = t.TransactionType,
                Amount = t.Amount,
                BalanceAfter = t.BalanceAfter,
                Reference = t.Reference,
                AuthorisedBy = t.AuthorisedBy
            })
            .ToList();

        return new PagedResult<EscrowTransactionDto>(items, totalCount, request.Page, request.PageSize);
    }
}
