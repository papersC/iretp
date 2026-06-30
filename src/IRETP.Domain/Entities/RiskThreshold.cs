using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class RiskThreshold : BaseEntity
{
    public string IndicatorKey { get; set; } = default!;
    public string IndicatorName { get; set; } = default!;
    public string IndicatorNameAr { get; set; } = default!;
    public decimal ThresholdValue { get; set; }
    public string ThresholdUnit { get; set; } = default!; // months, percentage, days, etc.
    public RiskLevel DefaultRiskLevel { get; set; }
    public AlertLevel DefaultAlertLevel { get; set; }
    public string? EscalationPath { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// JSON array of SOP checklist step titles associated with this risk
    /// indicator (RFP Section 8.3 — Playbook Integration). Shared across every
    /// RiskAlert emitted for this indicator; per-alert completion state is
    /// tracked on <see cref="RiskAlert.PlaybookProgressJson"/>.
    /// </summary>
    public string? PlaybookStepsJson { get; set; }
}
