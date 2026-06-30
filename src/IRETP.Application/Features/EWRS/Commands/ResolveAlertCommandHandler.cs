using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class ResolveAlertCommandHandler : IRequestHandler<ResolveAlertCommand, bool>
{
    private readonly IRepository<RiskAlert> _alertRepo;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveAlertCommandHandler(
        IRepository<RiskAlert> alertRepo,
        IUnitOfWork unitOfWork)
    {
        _alertRepo = alertRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ResolveAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _alertRepo.GetByIdAsync(request.AlertId, cancellationToken);
        if (alert is null)
            return false;

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.ResolvedBy = request.UserId;
        alert.ActionNotes = request.ActionNotes;

        _alertRepo.Update(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
