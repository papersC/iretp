using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class RiskAlert : BaseEntity
{
    public string IndicatorType { get; set; } = default!;
    public RiskLevel RiskLevel { get; set; }
    public AlertLevel AlertLevel { get; set; }
    public AlertStatus Status { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? DeveloperId { get; set; }
    public Developer? Developer { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? AssignedTo { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ActionNotes { get; set; }
    public string? EscalationPath { get; set; }

    /// <summary>
    /// Per-alert playbook checklist state — JSON array of
    /// { stepIndex, completedAt, completedBy, notes }. Initialised lazily the
    /// first time a step is checked off (RFP Section 8.3).
    /// </summary>
    public string? PlaybookProgressJson { get; set; }

    // RFP Section 8.2 — SLA tracking. Deadlines are set when the alert is
    // created from the AlertLevel and re-set when the alert escalates. The
    // auto-escalation job uses AcknowledgeDeadline to detect SLA breach.
    public DateTime? AcknowledgeDeadline { get; set; }
    public DateTime? ResolutionDeadline { get; set; }
    public DateTime? LastEscalatedAt { get; set; }
    public bool AutoEscalated { get; set; }
}
