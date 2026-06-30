using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Analytics.Queries;

public class ExecuteAnalyticsQuery : IRequest<AnalyticsResultDto>
{
    public AnalyticsQueryRequest Request { get; set; } = new();
}
