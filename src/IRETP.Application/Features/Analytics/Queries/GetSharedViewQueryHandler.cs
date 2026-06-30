using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Analytics.Queries;

public class GetSharedViewQueryHandler : IRequestHandler<GetSharedViewQuery, SavedAnalyticsViewDto?>
{
    private readonly IRepository<SavedAnalyticsView> _repository;

    public GetSharedViewQueryHandler(IRepository<SavedAnalyticsView> repository)
    {
        _repository = repository;
    }

    public async Task<SavedAnalyticsViewDto?> Handle(
        GetSharedViewQuery request, CancellationToken cancellationToken)
    {
        // RFP AN-006: tokens are valid for 12 months. Reject expired links
        // at the boundary so a stale URL can't resurrect old analysis state.
        var now = DateTime.UtcNow;
        var view = await _repository.Query()
            .Where(v => v.ShareToken == request.ShareToken
                        && v.IsPublic
                        && (v.ShareTokenExpiresAt == null || v.ShareTokenExpiresAt > now))
            .Select(v => new SavedAnalyticsViewDto
            {
                Id = v.Id,
                Name = v.Name,
                ConfigurationJson = v.ConfigurationJson,
                IsPublic = v.IsPublic,
                ShareToken = v.ShareToken,
                DisplayOrder = v.DisplayOrder,
                CreatedAt = v.CreatedAt,
                ShareTokenExpiresAt = v.ShareTokenExpiresAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return view;
    }
}
