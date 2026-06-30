using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Analytics.Commands;

public class DeleteSavedViewCommandHandler : IRequestHandler<DeleteSavedViewCommand, bool>
{
    private readonly IRepository<SavedAnalyticsView> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSavedViewCommandHandler(
        IRepository<SavedAnalyticsView> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteSavedViewCommand request, CancellationToken cancellationToken)
    {
        var view = await _repository.GetByIdAsync(request.Id, cancellationToken);

        if (view is null || view.UserId != request.UserId)
            return false;

        _repository.Remove(view);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
