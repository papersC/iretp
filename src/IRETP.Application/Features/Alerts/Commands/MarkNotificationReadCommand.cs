using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class MarkNotificationReadCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
}
