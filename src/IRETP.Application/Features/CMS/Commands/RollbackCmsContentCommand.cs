using MediatR;

namespace IRETP.Application.Features.CMS.Commands;

public class RollbackCmsContentCommand : IRequest<bool>
{
    public Guid CmsContentId { get; set; }
    public Guid VersionId { get; set; }
    public string? UserId { get; set; }
}
