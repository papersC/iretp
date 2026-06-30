using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Tests.Domain;

public class AlertSlaTests
{
    // Wednesday 2026-04-15 09:00 UAE (= 05:00 UTC) — well inside business hours.
    private static readonly DateTime BusinessHoursUtc =
        new DateTime(2026, 4, 15, 5, 0, 0, DateTimeKind.Utc);

    // Wednesday 2026-04-15 22:00 UAE (= 18:00 UTC) — after 17:00 UAE close.
    private static readonly DateTime AfterHoursUtc =
        new DateTime(2026, 4, 15, 18, 0, 0, DateTimeKind.Utc);

    // Friday 2026-04-17 12:00 UAE (= 08:00 UTC) — UAE weekend.
    private static readonly DateTime WeekendUtc =
        new DateTime(2026, 4, 17, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Level1_acks_in_4h_resolves_in_2d()
    {
        var (ack, res) = AlertSla.DeadlinesFor(AlertLevel.Level1_Operational, BusinessHoursUtc);

        Assert.Equal(BusinessHoursUtc.AddHours(4), ack);
        Assert.Equal(BusinessHoursUtc.AddDays(2), res);
    }

    [Fact]
    public void Level2_acks_in_2h_resolves_in_1d()
    {
        var (ack, res) = AlertSla.DeadlinesFor(AlertLevel.Level2_Managerial, BusinessHoursUtc);

        Assert.Equal(BusinessHoursUtc.AddHours(2), ack);
        Assert.Equal(BusinessHoursUtc.AddDays(1), res);
    }

    [Fact]
    public void Level3_business_hours_uses_1h_ack_and_4h_resolve()
    {
        var (ack, res) = AlertSla.DeadlinesFor(AlertLevel.Level3_SeniorLeadership, BusinessHoursUtc);

        Assert.Equal(BusinessHoursUtc.AddHours(1), ack);
        Assert.Equal(BusinessHoursUtc.AddHours(4), res);
    }

    [Fact]
    public void Level3_after_hours_uses_4h_ack_and_12h_resolve()
    {
        var (ack, res) = AlertSla.DeadlinesFor(AlertLevel.Level3_SeniorLeadership, AfterHoursUtc);

        Assert.Equal(AfterHoursUtc.AddHours(4), ack);
        Assert.Equal(AfterHoursUtc.AddHours(12), res);
    }

    [Fact]
    public void Level3_on_uae_weekend_uses_after_hours_window()
    {
        var (ack, res) = AlertSla.DeadlinesFor(AlertLevel.Level3_SeniorLeadership, WeekendUtc);

        // Friday 12:00 UAE is a weekend day per the helper — expect after-hours window.
        Assert.Equal(WeekendUtc.AddHours(4), ack);
        Assert.Equal(WeekendUtc.AddHours(12), res);
    }

    [Fact]
    public void Level4_strategic_carries_no_standard_sla()
    {
        var (ack, res) = AlertSla.DeadlinesFor(AlertLevel.Level4_Strategic, BusinessHoursUtc);

        Assert.Null(ack);
        Assert.Null(res);
    }
}
