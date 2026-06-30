using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Analytics.Queries;

public class ExecuteAnalyticsQueryHandler : IRequestHandler<ExecuteAnalyticsQuery, AnalyticsResultDto>
{
    private readonly IRepository<Transaction> _transactionRepo;

    public ExecuteAnalyticsQueryHandler(IRepository<Transaction> transactionRepo)
    {
        _transactionRepo = transactionRepo;
    }

    public async Task<AnalyticsResultDto> Handle(
        ExecuteAnalyticsQuery query, CancellationToken cancellationToken)
    {
        var req = query.Request;

        // Build filtered query with necessary includes
        var dbQuery = _transactionRepo.Query()
            .Include(t => t.Zone)
            .Include(t => t.Project!)
                .ThenInclude(p => p.Developer)
            .AsQueryable();

        // Apply filters
        if (req.DateFrom.HasValue)
            dbQuery = dbQuery.Where(t => t.TransactionDate >= req.DateFrom.Value);

        if (req.DateTo.HasValue)
            dbQuery = dbQuery.Where(t => t.TransactionDate <= req.DateTo.Value);

        if (req.ZoneIds is { Count: > 0 })
            dbQuery = dbQuery.Where(t => req.ZoneIds.Contains(t.ZoneId));

        if (req.PropertyTypes is { Count: > 0 })
        {
            var parsedTypes = req.PropertyTypes
                .Where(pt => Enum.TryParse<PropertyType>(pt, true, out _))
                .Select(pt => Enum.Parse<PropertyType>(pt, true))
                .ToList();
            if (parsedTypes.Count > 0)
                dbQuery = dbQuery.Where(t => parsedTypes.Contains(t.PropertyType));
        }

        if (req.TransactionTypes is { Count: > 0 })
        {
            var parsedTxTypes = req.TransactionTypes
                .Where(tt => Enum.TryParse<TransactionType>(tt, true, out _))
                .Select(tt => Enum.Parse<TransactionType>(tt, true))
                .ToList();
            if (parsedTxTypes.Count > 0)
                dbQuery = dbQuery.Where(t => parsedTxTypes.Contains(t.TransactionType));
        }

        // Materialize up to 50,000 rows for in-memory grouping
        var transactions = await dbQuery
            .OrderByDescending(t => t.TransactionDate)
            .Take(50_000)
            .ToListAsync(cancellationToken);

        // Default dimensions/metrics if none specified
        var dimensions = req.Dimensions.Count > 0 ? req.Dimensions : ["Zone"];
        var metrics = req.Metrics.Count > 0 ? req.Metrics : ["TransactionCount", "TotalValue"];

        // Group transactions by selected dimensions
        var grouped = transactions
            .GroupBy(t => BuildGroupKey(t, dimensions))
            .Select(g => BuildDataPoint(g, dimensions, metrics))
            .ToList();

        // Summary statistics across all transactions
        var summary = new Dictionary<string, decimal>
        {
            ["TotalTransactionCount"] = transactions.Count,
            ["TotalValue"] = transactions.Sum(t => t.TransactionValue),
            ["OverallAvgPricePerSqft"] = transactions.Count > 0
                ? Math.Round(transactions.Average(t => t.PricePerSqft), 2)
                : 0m
        };

        return new AnalyticsResultDto
        {
            Dimensions = dimensions.ToList(),
            Metrics = metrics.ToList(),
            RecommendedChartType = req.ChartType ?? RecommendChartType(dimensions, metrics),
            Data = grouped,
            SummaryStatistics = summary
        };
    }

    private static string BuildGroupKey(Transaction t, IEnumerable<string> dimensions)
    {
        var parts = dimensions.Select(d => GetDimensionValue(t, d));
        return string.Join("||", parts);
    }

    private static string GetDimensionValue(Transaction t, string dimension)
    {
        return dimension switch
        {
            "Zone" => t.Zone?.Name ?? "Unknown",
            "Developer" => t.Project?.Developer?.Name ?? "Unknown",
            "PropertyType" => t.PropertyType.ToString(),
            "TransactionType" => t.TransactionType.ToString(),
            "TimePeriod" => t.TransactionDate.ToString("yyyy-MM"),
            "ConstructionStatus" => t.Project?.Status.ToString() ?? "N/A",
            _ => "Unknown"
        };
    }

    private static Dictionary<string, object> BuildDataPoint(
        IGrouping<string, Transaction> group,
        IEnumerable<string> dimensions,
        IEnumerable<string> metrics)
    {
        var result = new Dictionary<string, object>();

        // Add dimension values
        var dimValues = group.Key.Split("||");
        var dimList = dimensions.ToList();
        for (var i = 0; i < dimList.Count; i++)
        {
            result[dimList[i]] = i < dimValues.Length ? dimValues[i] : "Unknown";
        }

        var items = group.ToList();

        // Add metric values
        foreach (var metric in metrics)
        {
            result[metric] = metric switch
            {
                "TransactionCount" => items.Count,
                "TotalValue" => Math.Round(items.Sum(t => t.TransactionValue), 2),
                "AvgPricePerSqft" => items.Count > 0
                    ? Math.Round(items.Average(t => t.PricePerSqft), 2)
                    : 0m,
                "RentalYield" => 0m, // Rental yield requires RentalIndex data; placeholder
                "UnitsCount" => items
                    .Where(t => t.Project != null)
                    .Select(t => t.Project!)
                    .DistinctBy(p => p.Id)
                    .Sum(p => p.TotalUnits),
                "CompletionPercentage" => items
                    .Where(t => t.Project != null)
                    .Select(t => t.Project!)
                    .DistinctBy(p => p.Id)
                    .DefaultIfEmpty()
                    .Average(p => p?.CompletionPercentage ?? 0m),
                _ => 0
            };
        }

        return result;
    }

    private static string RecommendChartType(IEnumerable<string> dimensions, IEnumerable<string> metrics)
    {
        var dimList = dimensions.ToList();
        var metricList = metrics.ToList();

        // Time-based dimension suggests a line chart
        if (dimList.Contains("TimePeriod"))
            return metricList.Count > 1 ? "Area" : "Line";

        // Single dimension with single metric
        if (dimList.Count == 1 && metricList.Count == 1)
            return "Bar";

        // Two dimensions suggest stacked bar
        if (dimList.Count >= 2)
            return "StackedBar";

        // Single dimension, multiple metrics
        if (dimList.Count == 1 && metricList.Count > 1)
            return "Bar";

        // No dimensions means summary
        if (dimList.Count == 0)
            return "KpiSummary";

        return "DataTable";
    }
}
