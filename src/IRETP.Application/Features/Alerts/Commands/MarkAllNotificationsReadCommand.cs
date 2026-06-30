using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class MarkAllNotificationsReadCommand : IRequest<int>
{
    public string? UserId { get; set; }
}
