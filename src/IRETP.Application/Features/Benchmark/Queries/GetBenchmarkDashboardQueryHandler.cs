using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Benchmark.Queries;

public class GetBenchmarkDashboardQueryHandler
    : IRequestHandler<GetBenchmarkDashboardQuery, BenchmarkDashboardDto>
{
    private readonly IRepository<MarketBenchmark> _repo;

    public GetBenchmarkDashboardQueryHandler(IRepository<MarketBenchmark> repo)
    {
        _repo = repo;
    }

    public async Task<BenchmarkDashboardDto> Handle(GetBenchmarkDashboardQuery request, CancellationToken ct)
    {
        var all = await _repo.Query().ToListAsync(ct);

        // Latest snapshot per city
        var latest = all
            .GroupBy(b => b.CityCode)
            .Select(g => g.OrderByDescending(b => b.Year).ThenByDescending(b => b.Quarter).First())
            .OrderBy(b => b.CityName)
            .ToList();

        var cities = latest.Select(b => new BenchmarkCityDto
        {
            CityCode = b.CityCode,
            CityName = b.CityName,
            CountryCode = b.CountryCode,
            Year = b.Year,
            Quarter = b.Quarter,
            GretiCompositeScore = b.GretiCompositeScore,
            AveragePricePerSqft = b.AveragePricePerSqft,
            AverageGrossRentalYieldPct = b.AverageGrossRentalYieldPct,
            PrimePriceYoYPct = b.PrimePriceYoYPct,
            TransactionVolumeYoYPct = b.TransactionVolumeYoYPct,
            InstitutionalCapitalSharePct = b.InstitutionalCapitalSharePct
        }).ToList();

        var matrix = new List<BenchmarkMetricRow>
        {
            BuildRow(cities, "GRETI composite score", "index",
                c => c.GretiCompositeScore),
            BuildRow(cities, "Prime price per sqft", "USD/sqft",
                c => c.AveragePricePerSqft),
            BuildRow(cities, "Average gross rental yield", "%",
                c => c.AverageGrossRentalYieldPct),
            BuildRow(cities, "Prime price change YoY", "%",
                c => c.PrimePriceYoYPct),
            BuildRow(cities, "Transaction volume YoY", "%",
                c => c.TransactionVolumeYoYPct),
            BuildRow(cities, "Institutional capital share", "%",
                c => c.InstitutionalCapitalSharePct),
        };

        return new BenchmarkDashboardDto
        {
            LastRefreshedAt = all.Count > 0 ? all.Max(b => b.UpdatedAt ?? b.CreatedAt) : DateTime.MinValue,
            Cities = cities,
            Matrix = matrix
        };
    }

    private static BenchmarkMetricRow BuildRow(
        List<BenchmarkCityDto> cities, string label, string unit,
        Func<BenchmarkCityDto, decimal> selector)
    {
        return new BenchmarkMetricRow
        {
            Metric = label,
            Unit = unit,
            ValuesByCity = cities.ToDictionary(c => c.CityCode, selector)
        };
    }
}
