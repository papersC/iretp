using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

/// <summary>
/// Daily FX snapshot published by the UAE Central Bank (RFP FR005 —
/// &quot;Currency switcher applying daily exchange rates from UAE Central Bank
/// API or equivalent&quot;). One row per (code, date). Historical rows are
/// preserved so audits and PDF exports can reproduce prior conversions.
/// </summary>
public class CurrencyRate : BaseEntity
{
    public string Code { get; set; } = default!;          // ISO 4217 e.g. "USD"
    public DateTime AsOfDate { get; set; }                // UTC midnight
    public decimal UnitsPerAed { get; set; }              // units of target per 1 AED
    public string Source { get; set; } = "UAECB";         // UAECB / IMF / manual / driftFallback
}
