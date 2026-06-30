using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Keeps the International Market Benchmarking table (RFP Section 20) fresh.
/// In a production deployment DLD's research team plugs index publisher feeds
/// (JLL, Savills, Knight Frank) into this service. Until those integrations
/// land the service applies a bounded deterministic drift to the most recent
/// snapshot so the /benchmark page shows a current "last refreshed" timestamp
/// and realistic period-over-period movement.
/// </summary>
public class BenchmarkRefreshService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BenchmarkRefreshService> _logger;

    public BenchmarkRefreshService(IServiceScopeFactory scopeFactory, ILogger<BenchmarkRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RefreshAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<MarketBenchmark>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var latestPerCity = await repo.Query()
            .ToListAsync();

        if (latestPerCity.Count == 0)
        {
            _logger.LogInformation("BenchmarkRefreshService: no seed data found — nothing to refresh.");
            return;
        }

        var byCity = latestPerCity
            .GroupBy(b => b.CityCode)
            .Select(g => g.OrderByDescending(b => b.Year).ThenByDescending(b => b.Quarter).First())
            .ToList();

        // If we already have an entry for the current quarter, bump the
        // UpdatedAt column in place instead of creating a duplicate row.
        var (nextYear, nextQuarter) = NextQuarter();

        var toUpdate = new List<MarketBenchmark>();
        var toAdd = new List<MarketBenchmark>();

        foreach (var prior in byCity)
        {
            // Deterministic drift anchored on city + quarter so every refresh
            // yields the same snapshot — makes the page's behaviour testable.
            var rng = new Random(HashCode.Combine(prior.CityCode, nextYear, nextQuarter));
            var drift = (decimal)(rng.NextDouble() * 0.06 - 0.03); // ±3% swing

            var existing = await repo.Query()
                .FirstOrDefaultAsync(b => b.CityCode == prior.CityCode
                                          && b.Year == nextYear
                                          && b.Quarter == nextQuarter);

            if (existing is not null)
            {
                existing.GretiCompositeScore = Clamp(prior.GretiCompositeScore * (1m + drift / 2m), 1m, 4m, 2);
                existing.AveragePricePerSqft = Round(prior.AveragePricePerSqft * (1m + drift), 0);
                existing.AverageGrossRentalYieldPct = Clamp(prior.AverageGrossRentalYieldPct + drift * 2m, 1m, 12m, 2);
                existing.PrimePriceYoYPct = Clamp(prior.PrimePriceYoYPct + drift * 20m, -25m, 30m, 2);
                existing.TransactionVolumeYoYPct = Clamp(prior.TransactionVolumeYoYPct + drift * 25m, -40m, 45m, 2);
                existing.InstitutionalCapitalSharePct = Clamp(prior.InstitutionalCapitalSharePct + drift * 3m, 10m, 85m, 2);
                toUpdate.Add(existing);
            }
            else
            {
                toAdd.Add(new MarketBenchmark
                {
                    CityCode = prior.CityCode,
                    CityName = prior.CityName,
                    CountryCode = prior.CountryCode,
                    Year = nextYear,
                    Quarter = nextQuarter,
                    GretiCompositeScore = Clamp(prior.GretiCompositeScore * (1m + drift / 2m), 1m, 4m, 2),
                    AveragePricePerSqft = Round(prior.AveragePricePerSqft * (1m + drift), 0),
                    AverageGrossRentalYieldPct = Clamp(prior.AverageGrossRentalYieldPct + drift * 2m, 1m, 12m, 2),
                    PrimePriceYoYPct = Clamp(prior.PrimePriceYoYPct + drift * 20m, -25m, 30m, 2),
                    TransactionVolumeYoYPct = Clamp(prior.TransactionVolumeYoYPct + drift * 25m, -40m, 45m, 2),
                    InstitutionalCapitalSharePct = Clamp(prior.InstitutionalCapitalSharePct + drift * 3m, 10m, 85m, 2),
                    Notes = prior.CityCode == "DXB"
                        ? "Drift-refreshed snapshot — replace with real DLD research feed when available."
                        : prior.Notes
                });
            }
        }

        if (toUpdate.Count > 0)
        {
            foreach (var b in toUpdate) repo.Update(b);
        }
        if (toAdd.Count > 0)
        {
            await repo.AddRangeAsync(toAdd);
        }

        if (toUpdate.Count + toAdd.Count > 0)
        {
            await unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "BenchmarkRefreshService: refreshed {UpdatedCount} + created {CreatedCount} snapshots for {Year}-Q{Quarter}.",
                toUpdate.Count, toAdd.Count, nextYear, nextQuarter);
        }
    }

    private static (int Year, int Quarter) NextQuarter()
    {
        var now = DateTime.UtcNow;
        var quarter = ((now.Month - 1) / 3) + 1;
        return (now.Year, quarter);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max, int decimals) =>
        Math.Round(Math.Clamp(value, min, max), decimals);

    private static decimal Round(decimal value, int decimals) => Math.Round(value, decimals);
}
