using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.CMS.Queries;

public class GetCmsVersionsQueryHandler : IRequestHandler<GetCmsVersionsQuery, List<CmsVersionDto>>
{
    private readonly IRepository<CmsContentVersion> _versionRepo;

    public GetCmsVersionsQueryHandler(IRepository<CmsContentVersion> versionRepo)
    {
        _versionRepo = versionRepo;
    }

    public async Task<List<CmsVersionDto>> Handle(
        GetCmsVersionsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _versionRepo.Query()
            .Where(v => v.CmsContentId == request.CmsContentId)
            .OrderByDescending(v => v.VersionNumber)
            .Take(Math.Clamp(request.Limit, 1, 200))
            .Select(v => new CmsVersionDto
            {
                Id = v.Id,
                CmsContentId = v.CmsContentId,
                VersionNumber = v.VersionNumber,
                ChangeType = v.ChangeType,
                ContentType = v.ContentType,
                ContentEn = v.ContentEn,
                ContentAr = v.ContentAr,
                SortOrder = v.SortOrder,
                Summary = v.Summary,
                CreatedBy = v.CreatedBy,
                CreatedAt = v.CreatedAt,
                HasActivePreview = v.PreviewToken != null
                                   && v.PreviewTokenExpiresAt != null
                                   && v.PreviewTokenExpiresAt > now
            })
            .ToListAsync(cancellationToken);
    }
}
