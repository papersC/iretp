namespace IRETP.Application.DTOs;

public class AnalyticsQueryRequest
{
    public List<string> Dimensions { get; set; } = []; // Zone, Developer, PropertyType, TransactionType, TimePeriod, ConstructionStatus
    public List<string> Metrics { get; set; } = []; // TransactionCount, TotalValue, AvgPricePerSqft, RentalYield, UnitsCount, CompletionPercentage
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public List<Guid>? ZoneIds { get; set; }
    public List<string>? PropertyTypes { get; set; }
    public List<string>? TransactionTypes { get; set; }
    public string? ChartType { get; set; } // Bar, StackedBar, Line, Area, Scatter, Donut, Treemap, DataTable, KpiSummary
}

public class AnalyticsResultDto
{
    public List<string> Dimensions { get; set; } = [];
    public List<string> Metrics { get; set; } = [];
    public string RecommendedChartType { get; set; } = "Bar";
    public List<Dictionary<string, object>> Data { get; set; } = [];
    public Dictionary<string, decimal> SummaryStatistics { get; set; } = new();
}

public class SavedAnalyticsViewDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string ConfigurationJson { get; set; } = default!;
    public bool IsPublic { get; set; }
    public string? ShareToken { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ShareTokenExpiresAt { get; set; }
}
