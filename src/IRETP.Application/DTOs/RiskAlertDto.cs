using IRETP.Domain.Enums;

namespace IRETP.Application.DTOs;

public class RiskAlertDto
{
    public Guid Id { get; set; }
    public string IndicatorType { get; set; } = default!;
    public RiskLevel RiskLevel { get; set; }
    public AlertLevel AlertLevel { get; set; }
    public AlertStatus Status { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? DeveloperId { get; set; }
    public string? DeveloperName { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string? AssignedTo { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ActionNotes { get; set; }
    public string? EscalationPath { get; set; }
    public string? PlaybookProgressJson { get; set; }
    public DateTime CreatedAt { get; set; }

    // RFP Section 8.2 — SLA tracking
    public DateTime? AcknowledgeDeadline { get; set; }
    public DateTime? ResolutionDeadline { get; set; }
    public DateTime? LastEscalatedAt { get; set; }
    public bool AutoEscalated { get; set; }
}
