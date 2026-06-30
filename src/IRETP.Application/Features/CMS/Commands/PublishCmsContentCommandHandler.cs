using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.CMS.Commands;

/// <summary>
/// Moves a staged CmsContent row from Draft to Published and stamps a
/// Published version snapshot. The Published snapshot is what DLD can
/// roll back to from the admin UI (FR002 — rollback in 2 clicks).
/// </summary>
public class PublishCmsContentCommandHandler : IRequestHandler<PublishCmsContentCommand, bool>
{
    private readonly IRepository<CmsContent> _cmsRepo;
    private readonly IRepository<CmsContentVersion> _versionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public PublishCmsContentCommandHandler(
        IRepository<CmsContent> cmsRepo,
        IRepository<CmsContentVersion> versionRepo,
        IUnitOfWork unitOfWork)
    {
        _cmsRepo = cmsRepo;
        _versionRepo = versionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(PublishCmsContentCommand request, CancellationToken cancellationToken)
    {
        var content = await _cmsRepo.GetByIdAsync(request.Id, cancellationToken);
        if (content == null) return false;

        content.IsPublished = true;
        content.PublishedAt = DateTime.UtcNow;
        content.PublishedBy = request.UserId;
        content.UpdatedAt = DateTime.UtcNow;
        content.UpdatedBy = request.UserId;

        _cmsRepo.Update(content);

        // Snapshot the published state. If the latest draft already matches
        // the new published content, upgrade its ChangeType in place —
        // otherwise insert a new row so history stays linear.
        var latest = await _versionRepo.Query()
            .Where(v => v.CmsContentId == content.Id)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.VersionNumber == content.Version
            && latest.ContentEn == content.ContentEn
            && latest.ContentAr == content.ContentAr)
        {
            latest.ChangeType = "Published";
            _versionRepo.Update(latest);
        }
        else
        {
            await _versionRepo.AddAsync(new CmsContentVersion
            {
                CmsContentId = content.Id,
                VersionNumber = content.Version,
                ChangeType = "Published",
                ContentType = content.ContentType,
                ContentEn = content.ContentEn,
                ContentAr = content.ContentAr,
                SortOrder = content.SortOrder,
                Summary = "Published to production",
                CreatedBy = request.UserId
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
