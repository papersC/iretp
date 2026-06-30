using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class DeleteInvestorAlertCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
}
