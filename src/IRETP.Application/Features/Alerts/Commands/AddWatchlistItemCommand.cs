using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class AddWatchlistItemCommand : IRequest<Guid>
{
    public string? UserId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ZoneId { get; set; }
    public Guid? DeveloperId { get; set; }
}
