using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class AddWatchlistItemCommandHandler
    : IRequestHandler<AddWatchlistItemCommand, Guid>
{
    private readonly IRepository<WatchlistItem> _watchlistRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AddWatchlistItemCommandHandler(
        IRepository<WatchlistItem> watchlistRepo,
        IUnitOfWork unitOfWork)
    {
        _watchlistRepo = watchlistRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(
        AddWatchlistItemCommand request, CancellationToken cancellationToken)
    {
        var item = new WatchlistItem
        {
            UserId = request.UserId ?? throw new ArgumentException("UserId is required.", nameof(request)),
            ProjectId = request.ProjectId,
            ZoneId = request.ZoneId,
            DeveloperId = request.DeveloperId
        };

        await _watchlistRepo.AddAsync(item, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
