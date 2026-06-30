using MediatR;

namespace IRETP.Application.Features.CMS.Commands;

public class PublishCmsContentCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
}
