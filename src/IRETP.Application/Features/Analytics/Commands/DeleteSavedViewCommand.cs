using MediatR;

namespace IRETP.Application.Features.Analytics.Commands;

public class DeleteSavedViewCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
}
