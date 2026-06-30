using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Analytics.Queries;

public class GetSavedViewsQuery : IRequest<List<SavedAnalyticsViewDto>>
{
    public string? UserId { get; set; }
}
