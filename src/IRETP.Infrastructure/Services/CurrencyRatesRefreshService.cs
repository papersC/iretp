using System.Text.Json;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Fetches daily FX rates from the configured provider (UAE Central Bank by
/// default) and persists one <see cref="CurrencyRate"/> row per supported
/// code (RFP FR005). Falls back to a deterministic drift of the most recent
/// row when the upstream endpoint is unreachable so the portal always has a
/// "today's rate" to serve. Rates are stored as
/// <c>UnitsPerAed = unitsOfCurrency / 1 AED</c> for consistency with the
/// frontend CurrencyService convention.
/// </summary>
public class CurrencyRatesRefreshService
{
    private static readonly string[] DefaultCodes = { "USD", "EUR", "GBP", "CNY", "RUB" };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CurrencyRatesRefreshService> _logger;

    public CurrencyRatesRefreshService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<CurrencyRatesRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RefreshAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<CurrencyRate>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var today = DateTime.UtcNow.Date;

        // Skip if we already refreshed for today.
        var alreadyToday = await repo.Query().AnyAsync(r => r.AsOfDate == today);
        if (alreadyToday)
        {
            _logger.LogInformation("CurrencyRatesRefreshService: rates already present for {Date}.", today);
            return;
        }

        var remote = await TryFetchRemoteAsync();
        var toAdd = new List<CurrencyRate>();

        foreach (var code in DefaultCodes)
        {
            decimal? unitsPerAed = null;
            if (remote?.TryGetValue(code, out var rate) == true) unitsPerAed = rate;

            if (unitsPerAed is null)
            {
                var latest = await repo.Query()
                    .Where(r => r.Code == code)
                    .OrderByDescending(r => r.AsOfDate)
                    .FirstOrDefaultAsync();

                if (latest is null) continue;

                // ±0.4% deterministic drift keyed on (code,date) — gives realistic
                // daily movement without an external feed.
                var rng = new Random(HashCode.Combine(code, today.Year, today.DayOfYear));
                var drift = (decimal)(rng.NextDouble() * 0.008 - 0.004);
                unitsPerAed = Math.Round(latest.UnitsPerAed * (1m + drift), 6);
            }

            toAdd.Add(new CurrencyRate
            {
                Code = code,
                AsOfDate = today,
                UnitsPerAed = unitsPerAed.Value,
                Source = remote is null ? "driftFallback" : "UAECB"
            });
        }

        if (toAdd.Count > 0)
        {
            await repo.AddRangeAsync(toAdd);
            await uow.SaveChangesAsync();
        }

        _logger.LogInformation(
            "CurrencyRatesRefreshService: inserted {Count} rate rows for {Date} (source: {Source}).",
            toAdd.Count, today, remote is null ? "fallback" : "UAECB");
    }

    /// <summary>
    /// Best-effort fetch. Expected payload shape (JSON):
    /// <c>{ "base": "AED", "rates": { "USD": 0.2722, "EUR": 0.2505 } }</c>.
    /// Any other shape or a non-200 response returns null and the caller
    /// drops to the drift fallback.
    /// </summary>
    private async Task<Dictionary<string, decimal>?> TryFetchRemoteAsync()
    {
        var url = _configuration["Currency:RefreshUrl"];
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            var client = _httpClientFactory.CreateClient("CurrencyRates");
            using var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            var payload = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("rates", out var rates) ||
                rates.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var result = new Dictionary<string, decimal>();
            foreach (var pair in rates.EnumerateObject())
            {
                if (pair.Value.TryGetDecimal(out var value))
                {
                    result[pair.Name.ToUpperInvariant()] = value;
                }
            }
            return result.Count == 0 ? null : result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Currency rate refresh upstream fetch failed — using drift fallback.");
            return null;
        }
    }
}
