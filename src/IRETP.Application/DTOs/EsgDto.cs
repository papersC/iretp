namespace IRETP.Application.DTOs;

/// <summary>
/// Surface of the public ESG/Sustainability module (RFP Section 20).
/// Designed so the same payload can back the summary cards, the GIS map
/// heatmap layer, and the project-level certification list.
/// </summary>
public class EsgDashboardDto
{
    public int TotalCertifiedProjects { get; set; }
    public int TotalProjects { get; set; }
    public decimal CertifiedCoveragePct { get; set; }
    public int TotalUnitsInCertifiedProjects { get; set; }

    public List<EsgSchemeSummary> BySchemes { get; set; } = [];
    public List<EsgLevelSummary> ByLevels { get; set; } = [];
    public List<EsgZoneItem> ByZones { get; set; } = [];
    public List<EsgCertifiedProjectDto> TopProjects { get; set; } = [];
}

public class EsgSchemeSummary
{
    public string Scheme { get; set; } = default!;
    public int Count { get; set; }
    public decimal AverageScorePct { get; set; }
}

public class EsgLevelSummary
{
    public string Level { get; set; } = default!;
    public int Count { get; set; }
}

public class EsgZoneItem
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public int ProjectCount { get; set; }
    public int CertifiedProjectCount { get; set; }
    public decimal CoveragePct { get; set; }
    public decimal AverageLevel { get; set; }
}

public class EsgCertifiedProjectDto
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
    public string? ProjectNameAr { get; set; }
    public string DeveloperName { get; set; } = default!;
    public string ZoneName { get; set; } = default!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Scheme { get; set; } = default!;
    public string Level { get; set; } = default!;
    public decimal? ScorePct { get; set; }
    public DateTime AwardedAt { get; set; }
    public string? CertificateNumber { get; set; }
}
