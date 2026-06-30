using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.CMS.Commands;

public class CreatePreviewLinkCommand : IRequest<CmsPreviewLinkDto?>
{
    public Guid VersionId { get; set; }
    public int TtlHours { get; set; } = 48;
    public string? UserId { get; set; }
}
