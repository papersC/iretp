using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Analytics.Queries;

public class GetSavedViewsQueryHandler : IRequestHandler<GetSavedViewsQuery, List<SavedAnalyticsViewDto>>
{
    private readonly IRepository<SavedAnalyticsView> _repository;

    public GetSavedViewsQueryHandler(IRepository<SavedAnalyticsView> repository)
    {
        _repository = repository;
    }

    public async Task<List<SavedAnalyticsViewDto>> Handle(
        GetSavedViewsQuery request, CancellationToken cancellationToken)
    {
        var views = await _repository.Query()
            .Where(v => v.UserId == request.UserId)
            .OrderBy(v => v.DisplayOrder)
            .ThenByDescending(v => v.CreatedAt)
            .Select(v => new SavedAnalyticsViewDto
            {
                Id = v.Id,
                Name = v.Name,
                ConfigurationJson = v.ConfigurationJson,
                IsPublic = v.IsPublic,
                ShareToken = v.ShareToken,
                DisplayOrder = v.DisplayOrder,
                CreatedAt = v.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return views;
    }
}
