using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Escrow.Queries;

public class GetProjectEscrowDetailQueryHandler
    : IRequestHandler<GetProjectEscrowDetailQuery, EscrowDashboardDto?>
{
    private readonly IRepository<EscrowAccount> _escrowRepo;

    public GetProjectEscrowDetailQueryHandler(IRepository<EscrowAccount> escrowRepo)
    {
        _escrowRepo = escrowRepo;
    }

    public async Task<EscrowDashboardDto?> Handle(
        GetProjectEscrowDetailQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _escrowRepo.FindAsync(
            e => e.ProjectId == request.ProjectId, cancellationToken);

        var escrow = accounts.FirstOrDefault();
        if (escrow is null)
            return null;

        // Query with navigation properties for project/developer names
        var result = _escrowRepo.Query()
            .Where(e => e.ProjectId == request.ProjectId)
            .Select(e => new EscrowDashboardDto
            {
                EscrowAccountId = e.Id,
                ProjectId = e.ProjectId,
                ProjectName = e.Project.Name,
                DeveloperName = e.Project.Developer.Name,
                AccountNumber = e.AccountNumber,
                BankName = e.BankName,
                CurrentBalance = e.CurrentBalance,
                RequiredMinimumBalance = e.RequiredMinimumBalance,
                TotalFundsReceived = e.TotalFundsReceived,
                TotalAuthorisedWithdrawals = e.TotalAuthorisedWithdrawals,
                RemainingConstructionCost = e.RemainingConstructionCost,
                Status = e.Status,
                AdequacyRatio = e.AdequacyRatio,
                StatusBadge = e.Status == EscrowStatus.Adequate ? "success"
                    : e.Status == EscrowStatus.Warning ? "warning"
                    : "danger"
            })
            .FirstOrDefault();

        return result;
    }
}
