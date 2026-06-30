using IRETP.Application.Common;
using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Audit.Queries;

public class GetAuditLogsQuery : IRequest<PagedResult<AuditLogDto>>
{
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Action { get; set; }
    public string? UserId { get; set; }
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
