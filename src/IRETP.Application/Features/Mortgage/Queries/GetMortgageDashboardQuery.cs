using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Mortgage.Queries;

public class GetMortgageDashboardQuery : IRequest<MortgageDashboardDto>
{
    public int? LookbackMonths { get; set; } = 24;
}
