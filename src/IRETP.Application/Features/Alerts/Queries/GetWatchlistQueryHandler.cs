using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Queries;

public class GetWatchlistQueryHandler
    : IRequestHandler<GetWatchlistQuery, List<WatchlistItemDto>>
{
    private readonly IRepository<WatchlistItem> _watchlistRepo;

    public GetWatchlistQueryHandler(IRepository<WatchlistItem> watchlistRepo)
    {
        _watchlistRepo = watchlistRepo;
    }

    public async Task<List<WatchlistItemDto>> Handle(
        GetWatchlistQuery request, CancellationToken cancellationToken)
    {
        var items = _watchlistRepo.Query()
            .Where(w => w.UserId == request.UserId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WatchlistItemDto
            {
                Id = w.Id,
                ProjectId = w.ProjectId,
                ProjectName = w.Project != null ? w.Project.Name : null,
                ZoneId = w.ZoneId,
                ZoneName = w.Zone != null ? w.Zone.Name : null,
                DeveloperId = w.DeveloperId,
                DeveloperName = w.Developer != null ? w.Developer.Name : null,
                CreatedAt = w.CreatedAt
            })
            .ToList();

        return await Task.FromResult(items);
    }
}
