using IRETP.Application.Common;
using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.AIAgent.Queries;

public class GetAiInteractionLogsQuery : IRequest<PagedResult<AiInteractionLogDto>>
{
    public string? Topic { get; set; }
    public string? Search { get; set; }
    public bool? WasRefusal { get; set; }
    public bool? Success { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
