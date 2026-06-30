using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class AcknowledgeAlertCommand : IRequest<bool>
{
    public Guid AlertId { get; set; }
    public string? UserId { get; set; }
}
