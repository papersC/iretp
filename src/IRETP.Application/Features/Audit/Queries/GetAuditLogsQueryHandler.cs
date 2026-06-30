using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Audit.Queries;

public class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, PagedResult<AuditLogDto>>
{
    private readonly IRepository<AuditLog> _auditRepo;

    public GetAuditLogsQueryHandler(IRepository<AuditLog> auditRepo)
    {
        _auditRepo = auditRepo;
    }

    public async Task<PagedResult<AuditLogDto>> Handle(
        GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _auditRepo.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(a => a.EntityType == request.EntityType);

        if (!string.IsNullOrWhiteSpace(request.EntityId))
            query = query.Where(a => a.EntityId == request.EntityId);

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(a => a.Action == request.Action);

        if (!string.IsNullOrWhiteSpace(request.UserId))
            query = query.Where(a => a.UserId == request.UserId);

        if (request.From.HasValue)
            query = query.Where(a => a.CreatedAt >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(a => a.CreatedAt <= request.To.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search;
            query = query.Where(a =>
                (a.EntityType != null && a.EntityType.Contains(term)) ||
                (a.EntityId != null && a.EntityId.Contains(term)) ||
                (a.UserName != null && a.UserName.Contains(term)) ||
                (a.NewValues != null && a.NewValues.Contains(term)) ||
                (a.OldValues != null && a.OldValues.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto
            {
                Id = a.Id,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Action = a.Action,
                UserId = a.UserId,
                UserName = a.UserName,
                OldValues = a.OldValues,
                NewValues = a.NewValues,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditLogDto>(items, total, request.Page, request.PageSize);
    }
}
