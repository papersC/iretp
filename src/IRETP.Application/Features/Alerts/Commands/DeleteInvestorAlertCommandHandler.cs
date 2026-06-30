using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class DeleteInvestorAlertCommandHandler
    : IRequestHandler<DeleteInvestorAlertCommand, bool>
{
    private readonly IRepository<InvestorAlert> _alertRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteInvestorAlertCommandHandler(
        IRepository<InvestorAlert> alertRepo,
        IUnitOfWork unitOfWork)
    {
        _alertRepo = alertRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(
        DeleteInvestorAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _alertRepo.GetByIdAsync(request.Id, cancellationToken);

        if (alert is null || alert.UserId != request.UserId)
            return false;

        _alertRepo.Remove(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
