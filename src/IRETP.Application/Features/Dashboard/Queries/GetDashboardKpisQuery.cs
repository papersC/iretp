using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Dashboard.Queries;

public class GetDashboardKpisQuery : IRequest<DashboardKpiDto>
{
    /// <summary>
    /// Bypass the 15-minute KPI cache and force live recomputation. Used by
    /// the background refresh job; public reads should leave this false so
    /// the dashboard hits the warm cache.
    /// </summary>
    public bool ForceRefresh { get; init; }
}
