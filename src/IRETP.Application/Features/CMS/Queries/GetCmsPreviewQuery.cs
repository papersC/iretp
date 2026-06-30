using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.CMS.Queries;

public class GetCmsPreviewQuery : IRequest<CmsPreviewContentDto?>
{
    public string Token { get; set; } = default!;
}
