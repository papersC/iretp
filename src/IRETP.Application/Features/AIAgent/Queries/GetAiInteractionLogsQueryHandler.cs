using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.AIAgent.Queries;

public class GetAiInteractionLogsQueryHandler
    : IRequestHandler<GetAiInteractionLogsQuery, PagedResult<AiInteractionLogDto>>
{
    private readonly IRepository<AiInteractionLog> _repo;

    public GetAiInteractionLogsQueryHandler(IRepository<AiInteractionLog> repo)
    {
        _repo = repo;
    }

    public async Task<PagedResult<AiInteractionLogDto>> Handle(
        GetAiInteractionLogsQuery request, CancellationToken cancellationToken)
    {
        var query = _repo.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Topic))
            query = query.Where(x => x.Topic == request.Topic);
        if (request.WasRefusal.HasValue)
            query = query.Where(x => x.WasRefusal == request.WasRefusal.Value);
        if (request.Success.HasValue)
            query = query.Where(x => x.Success == request.Success.Value);
        if (request.From.HasValue)
            query = query.Where(x => x.CreatedAt >= request.From.Value);
        if (request.To.HasValue)
            query = query.Where(x => x.CreatedAt <= request.To.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search;
            query = query.Where(x => x.Query.Contains(term) || (x.Answer != null && x.Answer.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(Math.Clamp(request.PageSize, 1, 200))
            .Select(x => new AiInteractionLogDto
            {
                Id = x.Id,
                SessionId = x.SessionId,
                UserId = x.UserId,
                Language = x.Language,
                Query = x.Query,
                Topic = x.Topic,
                Answer = x.Answer,
                SourceCitation = x.SourceCitation,
                ModelUsed = x.ModelUsed,
                WasRefusal = x.WasRefusal,
                LatencyMs = x.LatencyMs,
                Success = x.Success,
                ErrorMessage = x.ErrorMessage,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AiInteractionLogDto>(items, total, request.Page, request.PageSize);
    }
}
