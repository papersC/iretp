namespace IRETP.Application.DTOs;

/// <summary>
/// Aggregate view for the public GRETI Progress Tracker page. Fields reflect
/// the six JLL GRETI sub-indices listed in RFP Section 2.2 and show how much
/// of the IRETP delivery is actually operational in each area. Lower scores
/// = more transparent (JLL convention).
/// </summary>
public class GretiDashboardDto
{
    public decimal CompositeScore { get; set; }
    public decimal CompositeBaseline2022 { get; set; }
    public decimal CompositeTarget { get; set; }
    public decimal ProjectedLift { get; set; }
    public int GlobalRank2024 { get; set; }
    public string Tier { get; set; } = default!;
    public List<GretiSubIndexDto> SubIndices { get; set; } = [];
    public List<GretiTrajectoryPoint> Trajectory { get; set; } = [];
}

public class GretiSubIndexDto
{
    public string Name { get; set; } = default!;
    public int Weight { get; set; }
    public decimal Baseline2022 { get; set; }
    public decimal Current2024 { get; set; }
    public decimal Target { get; set; }
    public int DeliveredPct { get; set; }
    public string Phase { get; set; } = default!;
    public string IretpLever { get; set; } = default!;
}

public class GretiTrajectoryPoint
{
    public string Label { get; set; } = default!;
    public decimal Composite { get; set; }
    public decimal TierThreshold { get; set; }
    public bool IsProjection { get; set; }
}
