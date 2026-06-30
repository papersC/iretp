namespace IRETP.Application.DTOs;

public class PublicDeveloperScorecardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public int CompletedProjects { get; set; }
    public decimal OnTimeDeliveryPercentage { get; set; }
    public string ReraComplianceRating { get; set; } = default!; // Excellent, Good, Fair, Poor
    public int TotalUnitsDelivered { get; set; }
}
