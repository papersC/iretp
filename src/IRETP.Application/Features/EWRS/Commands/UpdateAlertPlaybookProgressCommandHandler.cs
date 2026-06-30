using System.Text.Json;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class UpdateAlertPlaybookProgressCommandHandler
    : IRequestHandler<UpdateAlertPlaybookProgressCommand, bool>
{
    private readonly IRepository<RiskAlert> _alertRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAlertPlaybookProgressCommandHandler(
        IRepository<RiskAlert> alertRepo, IUnitOfWork unitOfWork)
    {
        _alertRepo = alertRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdateAlertPlaybookProgressCommand request, CancellationToken ct)
    {
        var alert = await _alertRepo.GetByIdAsync(request.AlertId, ct);
        if (alert is null) return false;

        // Normalise — stamp completedBy / completedAt on server so UI cannot
        // spoof the audit metadata.
        var now = DateTime.UtcNow;
        foreach (var entry in request.Progress)
        {
            if (entry.Completed)
            {
                entry.CompletedAt ??= now;
                entry.CompletedBy ??= request.UpdatedBy;
            }
            else
            {
                entry.CompletedAt = null;
                entry.CompletedBy = null;
            }
        }

        alert.PlaybookProgressJson = JsonSerializer.Serialize(request.Progress);
        _alertRepo.Update(alert);
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
