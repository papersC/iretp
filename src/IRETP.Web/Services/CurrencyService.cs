namespace IRETP.Web.Services;

/// <summary>
/// Currency conversion against AED (base). Rates are seeded with representative
/// values; in production they would refresh from the UAE Central Bank API (per
/// FR005). The service exposes deterministic formatting so cards, charts, and
/// PDFs render consistently regardless of the user's locale.
/// </summary>
public class CurrencyService
{
    public sealed record Currency(string Code, string Symbol, string Name, decimal RateFromAed);

    // Rates = units of target currency per 1 AED (illustrative, refreshed by a
    // scheduled job in production).
    public static readonly IReadOnlyList<Currency> Supported = new List<Currency>
    {
        new("AED", "د.إ", "UAE Dirham",     1.0000m),
        new("USD", "$",   "US Dollar",      0.2722m),
        new("EUR", "€",   "Euro",           0.2505m),
        new("GBP", "£",   "Pound Sterling", 0.2140m),
        new("CNY", "¥",   "Chinese Yuan",   1.9700m),
        new("RUB", "₽",   "Russian Ruble", 25.3400m),
    };

    private readonly UiStateService _ui;

    public CurrencyService(UiStateService ui)
    {
        _ui = ui;
    }

    public Currency Current => Supported.FirstOrDefault(c => c.Code == _ui.CurrencyCode) ?? Supported[0];

    public decimal Convert(decimal amountAed) => amountAed * Current.RateFromAed;

    /// <summary>Format an AED value in the currently-selected currency using
    /// IRETP's human-friendly suffix scheme (K / M / B).</summary>
    public string Format(decimal amountAed)
    {
        var c = Current;
        var converted = amountAed * c.RateFromAed;
        var abs = Math.Abs(converted);
        string formatted;
        if (abs >= 1_000_000_000) formatted = $"{converted / 1_000_000_000:N2}B";
        else if (abs >= 1_000_000) formatted = $"{converted / 1_000_000:N2}M";
        else if (abs >= 1_000) formatted = $"{converted / 1_000:N1}K";
        else formatted = $"{converted:N0}";
        return $"{c.Symbol} {formatted}";
    }

    public string FormatExact(decimal amountAed)
    {
        var c = Current;
        var converted = amountAed * c.RateFromAed;
        return $"{c.Symbol} {converted:N0}";
    }
}
