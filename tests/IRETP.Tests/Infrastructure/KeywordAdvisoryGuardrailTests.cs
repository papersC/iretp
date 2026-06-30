using IRETP.Application.Interfaces;
using IRETP.Infrastructure.Services;

namespace IRETP.Tests.Infrastructure;

public class KeywordAdvisoryGuardrailTests
{
    private readonly IAdvisoryGuardrail _guard = new KeywordAdvisoryGuardrail();

    // ----- Positive triggers ------------------------------------------------

    [Theory]
    [InlineData("You should buy a unit in Damac Lagoons today.")]
    [InlineData("I recommend buying off-plan in Business Bay.")]
    [InlineData("This apartment is a great investment.")]
    [InlineData("This community is a guaranteed return for the next decade.")]
    public void Recommendation_phrases_are_blocked(string answer)
    {
        var reason = _guard.Validate(answer);
        Assert.NotNull(reason);
        Assert.Contains("investment-advice", reason);
    }

    [Theory]
    [InlineData("Prices will rise by 12% in 2027.")]
    [InlineData("I predict that JVC yields will overtake Marina by Q3.")]
    [InlineData("Dubai Marina is expected to reach AED 3,000/sqft by year-end.")]
    [InlineData("By 2027 prices will be higher than today in most sub-markets.")]
    [InlineData("Prices are expected to climb through the rest of the year.")]
    [InlineData("Yields are projected to compress as supply catches up.")]
    [InlineData("Our forecast for AED 3,400/sqft holds for JVC.")]
    public void Price_forecast_phrases_are_blocked(string answer)
    {
        var reason = _guard.Validate(answer);
        Assert.NotNull(reason);
        Assert.Contains("price-forecast", reason);
    }

    [Fact]
    public void Detection_is_case_insensitive()
    {
        var reason = _guard.Validate("YOU SHOULD BUY a villa in Palm Jumeirah");
        Assert.NotNull(reason);
    }

    // ----- Non-triggers (false-positive guards) -----------------------------

    [Theory]
    [InlineData("Dubai Marina recorded 1,243 transactions in March 2026 with an average price per sqft of AED 2,180.")]
    [InlineData("Damac Lagoons has 8 active off-plan projects with a combined launch value of AED 4.2bn.")]
    [InlineData("The gross rental yield in JVC for the latest quarter is 8.7% based on registered Ejari contracts.")]
    [InlineData("Historically, Business Bay prices appreciated 6% year-on-year over the last 5 years.")]
    public void Factual_data_answers_pass_through(string answer)
    {
        Assert.Null(_guard.Validate(answer));
    }

    [Fact]
    public void Empty_answer_passes_through()
    {
        Assert.Null(_guard.Validate(string.Empty));
        Assert.Null(_guard.Validate("   "));
    }

    [Fact]
    public void Mentioning_a_developer_name_alone_does_not_trigger()
    {
        // The guardrail must not fire on legitimate analytical answers that
        // happen to name a developer or property.
        var answer = "Emaar completed 12 projects in 2025, delivering 4,200 units across Downtown and Dubai Hills.";
        Assert.Null(_guard.Validate(answer));
    }
}
