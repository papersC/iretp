using IRETP.Domain.Enums;

namespace IRETP.Application.DTOs;

public class EwrsDashboardDto
{
    public int TotalHighRiskProjects { get; set; }
    public int TotalWarningProjects { get; set; }
    public int ProjectsWithEscrowShortfall { get; set; }
    public int ProjectsWithConstructionHalt { get; set; }
    public int TotalActiveAlerts { get; set; }
    public int UnacknowledgedAlerts { get; set; }
    public List<ZoneRiskSummaryDto> ZoneRiskSummary { get; set; } = [];
    public List<RiskAlertDto> RecentAlerts { get; set; } = [];
}

public class ZoneRiskSummaryDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public string ZoneNameAr { get; set; } = default!;
    public int HighRiskCount { get; set; }
    public int WarningCount { get; set; }
    public int TotalProjects { get; set; }
    public string? GeoJson { get; set; }
    public double? CenterLat { get; set; }
    public double? CenterLng { get; set; }
}
