namespace IRETP.Application.DTOs;

public class BenchmarkDashboardDto
{
    public DateTime LastRefreshedAt { get; set; }
    public List<BenchmarkCityDto> Cities { get; set; } = [];
    public List<BenchmarkMetricRow> Matrix { get; set; } = [];
}

public class BenchmarkCityDto
{
    public string CityCode { get; set; } = default!;
    public string CityName { get; set; } = default!;
    public string CountryCode { get; set; } = default!;
    public int Year { get; set; }
    public int Quarter { get; set; }

    public decimal GretiCompositeScore { get; set; }
    public decimal AveragePricePerSqft { get; set; }
    public decimal AverageGrossRentalYieldPct { get; set; }
    public decimal PrimePriceYoYPct { get; set; }
    public decimal TransactionVolumeYoYPct { get; set; }
    public decimal InstitutionalCapitalSharePct { get; set; }
}

public class BenchmarkMetricRow
{
    public string Metric { get; set; } = default!;
    public string Unit { get; set; } = default!;
    public Dictionary<string, decimal> ValuesByCity { get; set; } = new();
}
