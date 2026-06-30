using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Escrow.Queries;

public class GetEscrowDashboardQuery : IRequest<List<EscrowDashboardDto>>;
