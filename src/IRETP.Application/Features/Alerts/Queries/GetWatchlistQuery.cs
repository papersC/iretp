using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Alerts.Queries;

public class GetWatchlistQuery : IRequest<List<WatchlistItemDto>>
{
    public string? UserId { get; set; }
}
