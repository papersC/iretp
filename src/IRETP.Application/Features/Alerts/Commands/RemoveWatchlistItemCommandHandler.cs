using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class RemoveWatchlistItemCommandHandler
    : IRequestHandler<RemoveWatchlistItemCommand, bool>
{
    private readonly IRepository<WatchlistItem> _watchlistRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveWatchlistItemCommandHandler(
        IRepository<WatchlistItem> watchlistRepo,
        IUnitOfWork unitOfWork)
    {
        _watchlistRepo = watchlistRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(
        RemoveWatchlistItemCommand request, CancellationToken cancellationToken)
    {
        var item = await _watchlistRepo.GetByIdAsync(request.Id, cancellationToken);

        if (item is null || item.UserId != request.UserId)
            return false;

        _watchlistRepo.Remove(item);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
