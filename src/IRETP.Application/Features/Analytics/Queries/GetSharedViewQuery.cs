using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Analytics.Queries;

public class GetSharedViewQuery : IRequest<SavedAnalyticsViewDto?>
{
    public string ShareToken { get; set; } = default!;
}
