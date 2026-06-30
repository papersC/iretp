using IRETP.Domain.Enums;

namespace IRETP.Application.DTOs;

public class PriceIndexDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public string ZoneNameAr { get; set; } = default!;
    public PropertyType PropertyType { get; set; }
    public bool IsOffPlan { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public int? Month { get; set; }
    public decimal AveragePricePerSqft { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalValue { get; set; }
    public decimal? QuarterlyChange { get; set; }
    public decimal? AnnualChange { get; set; }
}

public class PriceIndexTrendDto
{
    public List<PriceIndexDto> DataPoints { get; set; } = [];
    public decimal CurrentAvgPricePerSqft { get; set; }
    public decimal? LatestQuarterlyChange { get; set; }
    public decimal? LatestAnnualChange { get; set; }
}

public class PriceIndexComparisonDto
{
    public List<ZonePriceSeriesDto> ZoneSeries { get; set; } = [];
}

public class ZonePriceSeriesDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public string ZoneNameAr { get; set; } = default!;
    public List<PriceIndexDto> DataPoints { get; set; } = [];
    public decimal LatestAvgPrice { get; set; }
    public decimal? QuarterlyChange { get; set; }
    public decimal? AnnualChange { get; set; }
}
