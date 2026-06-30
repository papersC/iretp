using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class EscalateAlertCommand : IRequest<bool>
{
    public Guid AlertId { get; set; }
    public string? UserId { get; set; }
    public string? EscalationNotes { get; set; }
}
