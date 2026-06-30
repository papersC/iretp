using IRETP.Application.DTOs;

namespace IRETP.Application.Interfaces;

/// <summary>
/// In-process cache of the dashboard KPI snapshot (RFP FR003 — refresh every
/// 15 minutes). Populated by <c>KpiSnapshotRefreshService</c> on a recurring
/// Hangfire schedule so the public homepage doesn't trigger expensive
/// aggregations on every request.
/// </summary>
public interface IKpiSnapshotCache
{
    /// <summary>The most recent snapshot, or null if none has been published yet.</summary>
    DashboardKpiDto? Current { get; }

    /// <summary>Replace the cached snapshot with a freshly computed value.</summary>
    void Set(DashboardKpiDto snapshot);
}
