using IRETP.Domain.Common;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Auto-escalates EWRS RiskAlerts that have breached their acknowledgement
/// SLA (RFP Section 8.2). Runs on a recurring schedule, picks up any alert
/// that is still in <see cref="AlertStatus.New"/> past its
/// <see cref="RiskAlert.AcknowledgeDeadline"/>, and bumps it one level. The
/// AlertDeliveryService re-fans-out the alert at the new level on its next
/// pass because we reset Status = New. Level 4 alerts never auto-escalate
/// further.
/// </summary>
public class AlertEscalationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertEscalationService> _logger;

    public AlertEscalationService(IServiceScopeFactory scopeFactory, ILogger<AlertEscalationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EscalateBreachedAlertsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var alertRepo = scope.ServiceProvider.GetRequiredService<IRepository<RiskAlert>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTime.UtcNow;
        var breached = await alertRepo.Query()
            .Where(a => a.Status == AlertStatus.New
                        && a.AlertLevel < AlertLevel.Level4_Strategic
                        && a.AcknowledgeDeadline != null
                        && a.AcknowledgeDeadline < now)
            .ToListAsync();

        if (breached.Count == 0) return;

        foreach (var alert in breached)
        {
            var previousLevel = alert.AlertLevel;
            alert.AlertLevel = (AlertLevel)((int)alert.AlertLevel + 1);

            var (ackBy, resolveBy) = AlertSla.DeadlinesFor(alert.AlertLevel, now);
            alert.AcknowledgeDeadline = ackBy;
            alert.ResolutionDeadline = resolveBy;
            alert.LastEscalatedAt = now;
            alert.AutoEscalated = true;

            // Reset Status so AlertDeliveryService re-fans-out at the new
            // level on its next pass. Without this the new recipients would
            // never receive the alert.
            alert.Status = AlertStatus.New;

            var note = $"Auto-escalated {previousLevel} → {alert.AlertLevel} at {now:u} (SLA breach)";
            alert.EscalationPath = string.IsNullOrEmpty(alert.EscalationPath)
                ? note
                : $"{alert.EscalationPath} | {note}";

            _logger.LogWarning(
                "EWRS alert {AlertId} auto-escalated {From} → {To} after SLA breach",
                alert.Id, previousLevel, alert.AlertLevel);
        }

        await unitOfWork.SaveChangesAsync();
    }
}
