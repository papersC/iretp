using System.Text.Json;
using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Commands;

public class UpdateScoringWeightsCommandHandler
    : IRequestHandler<UpdateScoringWeightsCommand, bool>
{
    private readonly IRepository<ScoringWeight> _weightRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService? _audit;

    public UpdateScoringWeightsCommandHandler(
        IRepository<ScoringWeight> weightRepo,
        IUnitOfWork unitOfWork,
        IAuditLogService? audit = null)
    {
        _weightRepo = weightRepo;
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<bool> Handle(
        UpdateScoringWeightsCommand request, CancellationToken cancellationToken)
    {
        // Validate that weights sum to 100%
        var totalWeight = request.Weights.Sum(w => w.Weight);
        if (totalWeight != 100m)
            throw new InvalidOperationException(
                $"Scoring weights must sum to 100%. Current sum: {totalWeight}%.");

        var existingWeights = await _weightRepo.GetAllAsync(cancellationToken);
        var weightLookup = existingWeights.ToDictionary(w => w.CriterionKey);

        // Snapshot old values for the cross-entity audit trail (RFP §9.1.2).
        var oldSnapshot = existingWeights.ToDictionary(w => w.CriterionKey, w => w.Weight);

        var now = DateTime.UtcNow;

        foreach (var update in request.Weights)
        {
            if (!weightLookup.TryGetValue(update.CriterionKey, out var existing))
                throw new InvalidOperationException(
                    $"Unknown scoring criterion: '{update.CriterionKey}'.");

            existing.Weight = update.Weight;
            existing.ModifiedBy = request.ModifiedBy;
            existing.ModifiedAt = now;
            existing.UpdatedBy = request.ModifiedBy;
            existing.UpdatedAt = now;
            _weightRepo.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Central append-only audit row keyed to the scoring-weights entity
        // so DLD internal audit can ask "every change by <user>" without
        // scanning each affected table's ModifiedBy column.
        if (_audit is not null)
        {
            var newSnapshot = request.Weights.ToDictionary(w => w.CriterionKey, w => w.Weight);
            await _audit.LogAsync(
                entityType: nameof(ScoringWeight),
                entityId: "weights-config",
                action: "Update",
                userId: request.ModifiedBy,
                userName: request.ModifiedBy,
                oldValues: JsonSerializer.Serialize(oldSnapshot),
                newValues: JsonSerializer.Serialize(newSnapshot),
                ct: cancellationToken);
        }

        return true;
    }
}
