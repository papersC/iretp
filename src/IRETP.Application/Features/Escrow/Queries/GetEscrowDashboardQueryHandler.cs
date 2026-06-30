using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Escrow.Queries;

public class GetEscrowDashboardQueryHandler
    : IRequestHandler<GetEscrowDashboardQuery, List<EscrowDashboardDto>>
{
    private readonly IRepository<EscrowAccount> _escrowRepo;

    public GetEscrowDashboardQueryHandler(IRepository<EscrowAccount> escrowRepo)
    {
        _escrowRepo = escrowRepo;
    }

    public Task<List<EscrowDashboardDto>> Handle(
        GetEscrowDashboardQuery request, CancellationToken cancellationToken)
    {
        var result = _escrowRepo.Query()
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
            .ToList();

        return Task.FromResult(result);
    }
}
