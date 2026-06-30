using IRETP.Application.Features.Dashboard.Queries;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Recurring job that warms the KPI snapshot cache (RFP FR003 — homepage
/// KPIs refresh every 15 minutes). Runs the same MediatR query the
/// dashboard uses, with ForceRefresh = true so the handler bypasses the
/// cache and recomputes from source data.
/// </summary>
public class KpiSnapshotRefreshService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KpiSnapshotRefreshService> _logger;

    public KpiSnapshotRefreshService(IServiceScopeFactory scopeFactory, ILogger<KpiSnapshotRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RefreshAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        try
        {
            var snapshot = await mediator.Send(new GetDashboardKpisQuery { ForceRefresh = true });
            _logger.LogInformation(
                "KPI snapshot refreshed at {RefreshedAt:u} — {Count} txns YTD",
                snapshot.RefreshedAt, (long)snapshot.TotalTransactionsCount.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KPI snapshot refresh failed; cache will continue serving the previous value.");
        }
    }
}
