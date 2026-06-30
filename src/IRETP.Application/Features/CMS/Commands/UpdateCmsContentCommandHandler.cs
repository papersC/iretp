using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.CMS.Commands;

/// <summary>
/// Upsert CMS content. Every write also inserts a <see cref="CmsContentVersion"/>
/// row so the FR002 12-month version history &amp; rollback requirement is
/// satisfied without schema changes elsewhere. The saved CmsContent always
/// reflects the latest draft; the version history carries the immutable
/// audit trail.
/// </summary>
public class UpdateCmsContentCommandHandler : IRequestHandler<UpdateCmsContentCommand, Guid>
{
    private readonly IRepository<CmsContent> _cmsRepo;
    private readonly IRepository<CmsContentVersion> _versionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCmsContentCommandHandler(
        IRepository<CmsContent> cmsRepo,
        IRepository<CmsContentVersion> versionRepo,
        IUnitOfWork unitOfWork)
    {
        _cmsRepo = cmsRepo;
        _versionRepo = versionRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(UpdateCmsContentCommand request, CancellationToken cancellationToken)
    {
        if (request.Id.HasValue)
        {
            var existing = await _cmsRepo.GetByIdAsync(request.Id.Value, cancellationToken);
            if (existing != null)
            {
                existing.ContentEn = request.ContentEn;
                existing.ContentAr = request.ContentAr;
                existing.ContentType = request.ContentType;
                existing.SortOrder = request.SortOrder;
                existing.Version++;
                existing.IsPublished = false; // Goes back to draft on edit
                existing.UpdatedBy = request.UserId;
                existing.UpdatedAt = DateTime.UtcNow;
                _cmsRepo.Update(existing);

                await PersistVersionAsync(existing, "Draft", request.ChangeSummary, request.UserId, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return existing.Id;
            }
        }

        var newContent = new CmsContent
        {
            PageKey = request.PageKey,
            SectionKey = request.SectionKey,
            ContentType = request.ContentType,
            ContentEn = request.ContentEn,
            ContentAr = request.ContentAr,
            SortOrder = request.SortOrder,
            IsPublished = false,
            Version = 1,
            CreatedBy = request.UserId
        };

        var created = await _cmsRepo.AddAsync(newContent, cancellationToken);
        await PersistVersionAsync(created, "Draft", request.ChangeSummary ?? "Initial draft", request.UserId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return created.Id;
    }

    private async Task PersistVersionAsync(
        CmsContent content, string changeType, string? summary, string? userId, CancellationToken ct)
    {
        await _versionRepo.AddAsync(new CmsContentVersion
        {
            CmsContentId = content.Id,
            VersionNumber = content.Version,
            ChangeType = changeType,
            ContentType = content.ContentType,
            ContentEn = content.ContentEn,
            ContentAr = content.ContentAr,
            SortOrder = content.SortOrder,
            Summary = summary,
            CreatedBy = userId
        }, ct);
    }
}
