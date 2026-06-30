using System.Globalization;

namespace IRETP.Web.Services;

/// <summary>
/// Circuit-scoped UI preferences: active language, active currency, and raised
/// events when the user changes either. Components subscribe to
/// <see cref="OnChange"/> and call <see cref="StateHasChanged"/> to re-render.
/// </summary>
public class UiStateService
{
    public UiStateService(IHttpContextAccessor? http = null)
    {
        // Initialize from the active culture (populated by RequestLocalization middleware
        // before the Blazor circuit boots). Falls back to English when the culture isn't
        // one we support.
        var name = CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName ?? "en";
        Language = SupportedLanguages.Contains(name) ? name : "en";

        // Currency preference persists via a simple cookie set by iretpUi.applyCurrency.
        var ctx = http?.HttpContext;
        if (ctx != null && ctx.Request.Cookies.TryGetValue("iretp.currency", out var code))
        {
            var allowed = new[] { "AED", "USD", "EUR", "GBP", "CNY", "RUB" };
            if (!string.IsNullOrWhiteSpace(code) && allowed.Contains(code))
                CurrencyCode = code;
        }
    }

    public string Language { get; private set; } = "en";
    public string CurrencyCode { get; private set; } = "AED";

    public bool IsRtl => Language is "ar" or "ur";

    /// <summary>
    /// Languages supported by the IRETP portal. Phase 1 delivers EN + AR
    /// fully; Phase 4 introduces the extended set (RFP Section 7). Listed in
    /// ISO 639-1 codes.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedLanguages =
        new[] { "en", "ar", "zh", "ru", "ur", "fr", "hi", "de" };

    public static readonly IReadOnlyDictionary<string, string> LanguageNames =
        new Dictionary<string, string>
        {
            ["en"] = "English",
            ["ar"] = "العربية",
            ["zh"] = "中文",
            ["ru"] = "Русский",
            ["ur"] = "اردو",
            ["fr"] = "Français",
            ["hi"] = "हिन्दी",
            ["de"] = "Deutsch"
        };

    public event Action? OnChange;

    public void SetLanguage(string language)
    {
        if (Language == language) return;
        Language = language;
        try
        {
            var culture = new CultureInfo(language);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch
        {
            // ignore — invalid culture string falls back to previous setting
        }
        OnChange?.Invoke();
    }

    public void SetCurrency(string code)
    {
        if (CurrencyCode == code) return;
        CurrencyCode = code;
        OnChange?.Invoke();
    }
}
