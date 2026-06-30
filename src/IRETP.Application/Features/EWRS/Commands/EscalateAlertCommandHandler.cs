using IRETP.Domain.Common;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class EscalateAlertCommandHandler : IRequestHandler<EscalateAlertCommand, bool>
{
    private readonly IRepository<RiskAlert> _alertRepo;
    private readonly IUnitOfWork _unitOfWork;

    public EscalateAlertCommandHandler(
        IRepository<RiskAlert> alertRepo,
        IUnitOfWork unitOfWork)
    {
        _alertRepo = alertRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(EscalateAlertCommand request, CancellationToken cancellationToken)
    {
        var alert = await _alertRepo.GetByIdAsync(request.AlertId, cancellationToken);
        if (alert is null)
            return false;

        // Increment alert level to next level (cap at Level4_Strategic)
        var now = DateTime.UtcNow;
        if (alert.AlertLevel < AlertLevel.Level4_Strategic)
        {
            alert.AlertLevel = (AlertLevel)((int)alert.AlertLevel + 1);
            var (ackBy, resolveBy) = AlertSla.DeadlinesFor(alert.AlertLevel, now);
            alert.AcknowledgeDeadline = ackBy;
            alert.ResolutionDeadline = resolveBy;
        }

        alert.LastEscalatedAt = now;
        alert.Status = AlertStatus.Escalated;
        alert.EscalationPath = string.IsNullOrEmpty(alert.EscalationPath)
            ? $"Escalated to {alert.AlertLevel} by {request.UserId} at {now:u}"
            : $"{alert.EscalationPath} | Escalated to {alert.AlertLevel} by {request.UserId} at {now:u}";

        if (!string.IsNullOrEmpty(request.EscalationNotes))
        {
            alert.ActionNotes = string.IsNullOrEmpty(alert.ActionNotes)
                ? request.EscalationNotes
                : $"{alert.ActionNotes}\n{request.EscalationNotes}";
        }

        _alertRepo.Update(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
