using System.Text.RegularExpressions;
using IRETP.Application.Interfaces;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Keyword/phrase-based implementation of <see cref="IAdvisoryGuardrail"/>.
/// Catches the highest-risk advisory patterns (recommendation imperatives,
/// price-forecast assertions, single-property buy/sell advice). Designed to
/// minimise false positives on legitimate analytical queries — the patterns
/// look for advisory framing, not the mention of a developer or zone name.
/// Production deployments can swap this implementation for a classifier-based
/// validator without touching the orchestrator.
/// </summary>
public class KeywordAdvisoryGuardrail : IAdvisoryGuardrail
{
    private static readonly string[] RecommendationPhrases =
    [
        "you should buy", "you should invest", "you should purchase", "you should sell",
        "i recommend buying", "i recommend purchasing", "i recommend selling",
        "i suggest buying", "i suggest purchasing",
        "best investment", "best property to buy", "guaranteed return",
        "is a good investment", "is a great investment", "is a safe investment"
    ];

    private static readonly string[] PriceForecastPhrases =
    [
        "will increase by", "will rise by", "will appreciate by",
        "is expected to reach aed", "will reach aed",
        "i predict", "i forecast", "the forecast is",
        "guaranteed appreciation", "guaranteed price increase"
    ];

    // Broader forecast patterns not covered by the phrase list. These match
    // future-tense price predictions that the narrow phrase list would miss —
    // e.g. "by 2027 prices will be", "expected to rise", "projected to climb".
    // Historical-framed statements are allowed through because §5.1 only bans
    // forward-looking predictions presented as facts.
    private static readonly (Regex Pattern, string Label)[] ForecastPatterns =
    {
        (new Regex(@"\bby\s+20\d{2}\s+(?:prices?|rents?|yields?)\s+will\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "future-price assertion"),
        (new Regex(@"\b(?:prices?|rents?|yields?)\s+(?:are|is)\s+expected\s+to\s+(?:rise|climb|jump|grow|increase|reach|appreciate)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "forward-looking expectation"),
        (new Regex(@"\b(?:prices?|rents?|yields?)\s+(?:are|is)\s+projected\s+to\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "forward-looking projection"),
        (new Regex(@"\bforecast\s+(?:of|for)\s+(?:aed\s*)?[\d,\.]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "numeric forecast"),
    };

    public string? Validate(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return null;

        var lowered = answer.ToLowerInvariant();

        foreach (var phrase in RecommendationPhrases)
        {
            if (lowered.Contains(phrase))
                return $"investment-advice phrase: '{phrase}'";
        }

        foreach (var phrase in PriceForecastPhrases)
        {
            if (lowered.Contains(phrase))
                return $"price-forecast phrase: '{phrase}'";
        }

        foreach (var (pattern, label) in ForecastPatterns)
        {
            var match = pattern.Match(answer);
            if (match.Success)
                return $"price-forecast pattern ({label}): '{match.Value.Trim()}'";
        }

        return null;
    }
}
