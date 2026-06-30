using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.CMS.Queries;

public class GetCmsPreviewQueryHandler : IRequestHandler<GetCmsPreviewQuery, CmsPreviewContentDto?>
{
    private readonly IRepository<CmsContentVersion> _versionRepo;
    private readonly IRepository<CmsContent> _cmsRepo;

    public GetCmsPreviewQueryHandler(
        IRepository<CmsContentVersion> versionRepo, IRepository<CmsContent> cmsRepo)
    {
        _versionRepo = versionRepo;
        _cmsRepo = cmsRepo;
    }

    public async Task<CmsPreviewContentDto?> Handle(
        GetCmsPreviewQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) return null;

        var now = DateTime.UtcNow;
        var version = await _versionRepo.Query()
            .FirstOrDefaultAsync(v => v.PreviewToken == request.Token
                                      && v.PreviewTokenExpiresAt != null
                                      && v.PreviewTokenExpiresAt > now,
                                  cancellationToken);

        if (version is null) return null;

        var content = await _cmsRepo.GetByIdAsync(version.CmsContentId, cancellationToken);
        if (content is null) return null;

        return new CmsPreviewContentDto
        {
            CmsContentId = content.Id,
            PageKey = content.PageKey,
            SectionKey = content.SectionKey,
            ContentType = version.ContentType,
            ContentEn = version.ContentEn,
            ContentAr = version.ContentAr,
            VersionNumber = version.VersionNumber,
            SnapshotAt = version.CreatedAt,
            ExpiresAt = version.PreviewTokenExpiresAt!.Value
        };
    }
}
