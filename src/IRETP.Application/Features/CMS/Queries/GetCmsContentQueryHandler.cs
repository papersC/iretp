using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.CMS.Queries;

public class GetCmsContentQueryHandler : IRequestHandler<GetCmsContentQuery, List<CmsContentDto>>
{
    private readonly IRepository<CmsContent> _cmsRepo;

    public GetCmsContentQueryHandler(IRepository<CmsContent> cmsRepo)
    {
        _cmsRepo = cmsRepo;
    }

    public async Task<List<CmsContentDto>> Handle(GetCmsContentQuery request, CancellationToken cancellationToken)
    {
        var query = _cmsRepo.Query().AsQueryable();

        if (!string.IsNullOrEmpty(request.PageKey))
            query = query.Where(c => c.PageKey == request.PageKey);

        if (!string.IsNullOrEmpty(request.SectionKey))
            query = query.Where(c => c.SectionKey == request.SectionKey);

        if (request.PublishedOnly)
            query = query.Where(c => c.IsPublished);

        var data = await query
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        return data.Select(c => new CmsContentDto
        {
            Id = c.Id,
            PageKey = c.PageKey,
            SectionKey = c.SectionKey,
            ContentType = c.ContentType,
            Content = request.Locale == "ar" ? c.ContentAr : c.ContentEn,
            SortOrder = c.SortOrder,
            IsPublished = c.IsPublished,
            PublishedAt = c.PublishedAt,
            Version = c.Version
        }).ToList();
    }
}
