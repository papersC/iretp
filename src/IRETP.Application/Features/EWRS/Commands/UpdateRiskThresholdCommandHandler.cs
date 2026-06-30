using System.Text.Json;
using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class UpdateRiskThresholdCommandHandler : IRequestHandler<UpdateRiskThresholdCommand, bool>
{
    private readonly IRepository<RiskThreshold> _thresholdRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService? _audit;

    public UpdateRiskThresholdCommandHandler(
        IRepository<RiskThreshold> thresholdRepo,
        IUnitOfWork unitOfWork,
        IAuditLogService? audit = null)
    {
        _thresholdRepo = thresholdRepo;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<bool> Handle(UpdateRiskThresholdCommand request, CancellationToken cancellationToken)
    {
        var threshold = await _thresholdRepo.GetByIdAsync(request.ThresholdId, cancellationToken);
        if (threshold is null)
            return false;

        var before = new
        {
            threshold.ThresholdValue,
            threshold.DefaultRiskLevel,
            threshold.DefaultAlertLevel
        };

        threshold.ThresholdValue = request.ThresholdValue;
        threshold.DefaultRiskLevel = request.DefaultRiskLevel;
        threshold.DefaultAlertLevel = request.DefaultAlertLevel;
        threshold.ModifiedBy = request.UserId;
        threshold.ModifiedAt = DateTime.UtcNow;

        _thresholdRepo.Update(threshold);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // RFP §8.3 — append-only audit row keyed to the threshold so DLD
        // internal audit can trace every threshold adjustment by actor.
        if (_audit is not null)
        {
            var after = new
            {
                threshold.ThresholdValue,
                threshold.DefaultRiskLevel,
                threshold.DefaultAlertLevel
            };
            await _audit.LogAsync(
                entityType: nameof(RiskThreshold),
                entityId: threshold.Id.ToString(),
                action: "Update",
                userId: request.UserId,
                userName: request.UserId,
                oldValues: JsonSerializer.Serialize(before),
                newValues: JsonSerializer.Serialize(after),
                ct: cancellationToken);
        }

        return true;
    }
}
