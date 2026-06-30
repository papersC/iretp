using IRETP.Application.Common;
using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Escrow.Queries;

public class GetEscrowAuditLogQuery : IRequest<PagedResult<EscrowTransactionDto>>
{
    public Guid ProjectId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
