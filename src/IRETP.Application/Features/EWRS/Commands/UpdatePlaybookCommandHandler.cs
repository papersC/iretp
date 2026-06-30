using System.Text.Json;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class UpdatePlaybookCommandHandler : IRequestHandler<UpdatePlaybookCommand, bool>
{
    private readonly IRepository<RiskThreshold> _thresholdRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdatePlaybookCommandHandler(IRepository<RiskThreshold> thresholdRepo, IUnitOfWork unitOfWork)
    {
        _thresholdRepo = thresholdRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(UpdatePlaybookCommand request, CancellationToken cancellationToken)
    {
        var threshold = await _thresholdRepo.GetByIdAsync(request.ThresholdId);
        if (threshold is null) return false;

        var cleaned = request.Steps
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();

        threshold.PlaybookStepsJson = JsonSerializer.Serialize(cleaned);
        threshold.ModifiedBy = request.ModifiedBy;
        threshold.ModifiedAt = DateTime.UtcNow;

        _thresholdRepo.Update(threshold);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
