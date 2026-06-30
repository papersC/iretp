using IRETP.Application.DTOs;
using IRETP.Application.Interfaces;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Process-local KPI cache. Singleton — one snapshot shared across all
/// requests. Atomic reference swap means readers never see a partially
/// rebuilt DTO.
/// </summary>
public class KpiSnapshotCache : IKpiSnapshotCache
{
    private DashboardKpiDto? _current;

    public DashboardKpiDto? Current => Volatile.Read(ref _current);

    public void Set(DashboardKpiDto snapshot)
    {
        snapshot.RefreshedAt = DateTime.UtcNow;
        Volatile.Write(ref _current, snapshot);
    }
}
