using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ValidationEntity = IRETP.Domain.Entities.NameValidation;

namespace IRETP.Application.Features.NameValidation.Queries;

public class GetNameValidationsQueryHandler
    : IRequestHandler<GetNameValidationsQuery, PagedResult<NameValidationDto>>
{
    private readonly IRepository<ValidationEntity> _repo;

    public GetNameValidationsQueryHandler(IRepository<ValidationEntity> repo)
    {
        _repo = repo;
    }

    public async Task<PagedResult<NameValidationDto>> Handle(
        GetNameValidationsQuery request, CancellationToken ct)
    {
        var query = _repo.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(x => x.EntityType == request.EntityType);
        if (request.Status.HasValue)
            query = query.Where(x => (int)x.Status == request.Status.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search;
            query = query.Where(x =>
                x.NameEn.Contains(term) || x.NameAr.Contains(term)
                || (x.OfficialNameAr != null && x.OfficialNameAr.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.EntityType).ThenBy(x => x.Status).ThenBy(x => x.NameEn)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(Math.Clamp(request.PageSize, 1, 200))
            .Select(x => new NameValidationDto
            {
                Id = x.Id,
                EntityType = x.EntityType,
                EntityId = x.EntityId,
                NameEn = x.NameEn,
                NameAr = x.NameAr,
                OfficialNameAr = x.OfficialNameAr,
                Status = (int)x.Status,
                ReviewerName = x.ReviewerName,
                ReviewedAt = x.ReviewedAt,
                ReviewNotes = x.ReviewNotes,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<NameValidationDto>(items, total, request.Page, request.PageSize);
    }
}

public class GetNameValidationSummaryQueryHandler
    : IRequestHandler<GetNameValidationSummaryQuery, NameValidationSummaryDto>
{
    private readonly IRepository<ValidationEntity> _repo;

    public GetNameValidationSummaryQueryHandler(IRepository<ValidationEntity> repo)
    {
        _repo = repo;
    }

    public async Task<NameValidationSummaryDto> Handle(
        GetNameValidationSummaryQuery request, CancellationToken ct)
    {
        var rows = await _repo.Query().AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = (int)g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(NameValidationStatus s) => rows.FirstOrDefault(r => r.Status == (int)s)?.Count ?? 0;

        return new NameValidationSummaryDto
        {
            TotalEntities = rows.Sum(r => r.Count),
            Pending = Count(NameValidationStatus.Pending),
            Validated = Count(NameValidationStatus.Validated),
            Rejected = Count(NameValidationStatus.Rejected),
            NeedsCorrection = Count(NameValidationStatus.NeedsCorrection)
        };
    }
}
