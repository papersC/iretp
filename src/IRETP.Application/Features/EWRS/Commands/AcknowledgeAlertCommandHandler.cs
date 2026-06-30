using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class AcknowledgeAlertCommandHandler : IRequestHandler<AcknowledgeAlertCommand, bool>
{
    private readonly IRepository<RiskAlert> _alertRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AcknowledgeAlertCommandHandler(
        IRepository<RiskAlert> alertRepo,
        IUnitOfWork unitOfWork)
    {
        _alertRepo = alertRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AcknowledgeAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _alertRepo.GetByIdAsync(request.AlertId, cancellationToken);
        if (alert is null)
            return false;

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedBy = request.UserId;

        _alertRepo.Update(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
