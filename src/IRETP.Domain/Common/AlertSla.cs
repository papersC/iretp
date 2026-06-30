using IRETP.Domain.Enums;

namespace IRETP.Domain.Common;

/// <summary>
/// SLA windows per AlertLevel from RFP Section 8.2 (Multi-Level Alert
/// Escalation Framework). Time-of-day aware for Level 3 (1h business hours,
/// 4h after-hours). Level 4 carries no standard SLA — alerts are flagged for
/// immediate handling and never auto-escalate further.
/// </summary>
public static class AlertSla
{
    /// <summary>
    /// Returns the (acknowledge, resolution) deadlines for an alert created
    /// at <paramref name="alertLevel"/> at <paramref name="createdAtUtc"/>.
    /// </summary>
    public static (DateTime? Acknowledge, DateTime? Resolution) DeadlinesFor(
        AlertLevel alertLevel, DateTime createdAtUtc)
    {
        return alertLevel switch
        {
            AlertLevel.Level1_Operational => (
                createdAtUtc.AddHours(4),     // ack within 4 business hours
                createdAtUtc.AddDays(2)),     // action plan within 2 business days

            AlertLevel.Level2_Managerial => (
                createdAtUtc.AddHours(2),     // ack within 2 business hours
                createdAtUtc.AddDays(1)),     // escalation decision within 1 business day

            AlertLevel.Level3_SeniorLeadership => (
                createdAtUtc.AddHours(IsBusinessHours(createdAtUtc) ? 1 : 4),
                createdAtUtc.AddHours(IsBusinessHours(createdAtUtc) ? 4 : 12)),

            AlertLevel.Level4_Strategic => (null, null), // immediate — no standard SLA

            _ => (null, null)
        };
    }

    /// <summary>
    /// Sunday–Thursday 08:00–17:00 UAE (UTC+4). Used to pick the L3 ack
    /// window. We don't subtract holidays here — the SLA spec uses calendar
    /// hours for L3 specifically.
    /// </summary>
    private static bool IsBusinessHours(DateTime utc)
    {
        var local = utc.AddHours(4); // UAE Standard Time
        if (local.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday) return false;
        return local.Hour >= 8 && local.Hour < 17;
    }
}
