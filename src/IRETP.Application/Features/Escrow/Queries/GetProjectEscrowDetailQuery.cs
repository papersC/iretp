using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Escrow.Queries;

public class GetProjectEscrowDetailQuery : IRequest<EscrowDashboardDto?>
{
    public Guid ProjectId { get; set; }
}
