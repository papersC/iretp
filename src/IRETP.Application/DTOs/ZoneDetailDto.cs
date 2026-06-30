namespace IRETP.Application.DTOs;

public class ZoneDetailDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public string ZoneNameAr { get; set; } = default!;
    public int TotalTransactions12Months { get; set; }
    public decimal AverageSalePricePerSqft { get; set; }
    public Dictionary<string, decimal> AverageRentByUnitType { get; set; } = new();
    public decimal AverageGrossRentalYield { get; set; }
    public string PriceTrend { get; set; } = "stable"; // up, down, stable
    public decimal PriceTrendPercentage { get; set; }
    public List<TopDeveloperInfo> TopDevelopers { get; set; } = [];
    public int ActiveOffPlanProjects { get; set; }
    public int CompletedProjects { get; set; }
}

public class TopDeveloperInfo
{
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public int ProjectCount { get; set; }
}
