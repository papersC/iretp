using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class ResolveAlertCommand : IRequest<bool>
{
    public Guid AlertId { get; set; }
    public string? UserId { get; set; }
    public string? ActionNotes { get; set; }
}
