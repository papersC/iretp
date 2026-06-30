using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.CMS.Queries;

public class GetCmsVersionsQuery : IRequest<List<CmsVersionDto>>
{
    public Guid CmsContentId { get; set; }
    public int Limit { get; set; } = 50;
}
