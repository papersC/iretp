using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.CMS.Commands;

/// <summary>
/// Restore a <see cref="CmsContent"/> row to a historical version. Creates a
/// new version rather than mutating history — the RFP FR002 requirement is
/// &quot;rollback within 2 clicks&quot;, and the new version inherits the
/// rolled-back content but keeps the audit trail intact. The content returns
/// to Draft state so DLD still has to click Publish to push it live.
/// </summary>
public class RollbackCmsContentCommandHandler : IRequestHandler<RollbackCmsContentCommand, bool>
{
    private readonly IRepository<CmsContent> _cmsRepo;
    private readonly IRepository<CmsContentVersion> _versionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RollbackCmsContentCommandHandler(
        IRepository<CmsContent> cmsRepo,
        IRepository<CmsContentVersion> versionRepo,
        IUnitOfWork unitOfWork)
    {
        _cmsRepo = cmsRepo;
        _versionRepo = versionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(RollbackCmsContentCommand request, CancellationToken cancellationToken)
    {
        var content = await _cmsRepo.GetByIdAsync(request.CmsContentId, cancellationToken);
        if (content is null) return false;

        var target = await _versionRepo.GetByIdAsync(request.VersionId, cancellationToken);
        if (target is null || target.CmsContentId != content.Id) return false;

        content.ContentType = target.ContentType;
        content.ContentEn = target.ContentEn;
        content.ContentAr = target.ContentAr;
        content.SortOrder = target.SortOrder;
        content.Version++;
        content.IsPublished = false;
        content.UpdatedAt = DateTime.UtcNow;
        content.UpdatedBy = request.UserId;
        _cmsRepo.Update(content);

        await _versionRepo.AddAsync(new CmsContentVersion
        {
            CmsContentId = content.Id,
            VersionNumber = content.Version,
            ChangeType = "Rollback",
            ContentType = content.ContentType,
            ContentEn = content.ContentEn,
            ContentAr = content.ContentAr,
            SortOrder = content.SortOrder,
            Summary = $"Rolled back to v{target.VersionNumber} ({target.ChangeType})",
            CreatedBy = request.UserId
        }, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
