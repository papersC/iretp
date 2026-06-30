using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using ValidationEntity = IRETP.Domain.Entities.NameValidation;

namespace IRETP.Application.Features.NameValidation.Commands;

public class ReviewNameValidationCommandHandler : IRequestHandler<ReviewNameValidationCommand, bool>
{
    private readonly IRepository<ValidationEntity> _repo;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewNameValidationCommandHandler(
        IRepository<ValidationEntity> repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ReviewNameValidationCommand request, CancellationToken cancellationToken)
    {
        var row = await _repo.GetByIdAsync(request.Id, cancellationToken);
        if (row is null) return false;

        row.Status = (NameValidationStatus)Math.Clamp(request.Status, 0, 3);
        row.OfficialNameAr = string.IsNullOrWhiteSpace(request.OfficialNameAr) ? null : request.OfficialNameAr.Trim();
        row.ReviewNotes = string.IsNullOrWhiteSpace(request.ReviewNotes) ? null : request.ReviewNotes.Trim();
        row.ReviewerId = request.ReviewerId;
        row.ReviewerName = request.ReviewerName;
        row.ReviewedAt = DateTime.UtcNow;

        _repo.Update(row);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
