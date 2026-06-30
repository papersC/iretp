using IRETP.Application.Features.EWRS.Commands;
using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Repositories;
using IRETP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Tests.Application;

/// <summary>
/// §8.3 Threshold Configuration Panel — verifies that a threshold edit
/// persists the new values and stamps ModifiedBy + ModifiedAt (the audit
/// trail DLD relies on to reconstruct who changed a risk setting and when).
/// </summary>
public class UpdateRiskThresholdCommandHandlerTests
{
    private static (IretpDbContext db, UpdateRiskThresholdCommandHandler handler, RiskThreshold seeded) Build()
    {
        var options = new DbContextOptionsBuilder<IretpDbContext>()
            .UseInMemoryDatabase(databaseName: $"thresholds-{Guid.NewGuid():N}")
            .Options;
        var db = new IretpDbContext(options);
        var seeded = new RiskThreshold
        {
            Id = Guid.NewGuid(),
            IndicatorKey = "ProjectDeliveryDelay_Critical",
            IndicatorName = "Project Delivery Delay — Critical",
            IndicatorNameAr = "تأخير تسليم المشروع — حرج",
            ThresholdValue = 12m,
            ThresholdUnit = "months",
            DefaultRiskLevel = RiskLevel.High,
            DefaultAlertLevel = AlertLevel.Level3_SeniorLeadership
        };
        db.RiskThresholds.Add(seeded);
        db.SaveChanges();

        var repo = new Repository<RiskThreshold>(db);
        var uow = new UnitOfWork(db);
        IAuditLogService audit = new AuditLogService(new Repository<AuditLog>(db), uow);
        return (db, new UpdateRiskThresholdCommandHandler(repo, uow, audit), seeded);
    }

    [Fact]
    public async Task Updates_value_and_stamps_auditor_identity()
    {
        var (db, handler, seeded) = Build();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var ok = await handler.Handle(new UpdateRiskThresholdCommand
        {
            ThresholdId = seeded.Id,
            ThresholdValue = 9m,
            DefaultRiskLevel = RiskLevel.High,
            DefaultAlertLevel = AlertLevel.Level3_SeniorLeadership,
            UserId = "supervisor@dld.gov.ae"
        }, CancellationToken.None);

        Assert.True(ok);
        var reloaded = await db.RiskThresholds.AsNoTracking().SingleAsync();
        Assert.Equal(9m, reloaded.ThresholdValue);
        Assert.Equal("supervisor@dld.gov.ae", reloaded.ModifiedBy);
        Assert.NotNull(reloaded.ModifiedAt);
        Assert.InRange(reloaded.ModifiedAt!.Value, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Returns_false_when_threshold_does_not_exist()
    {
        var (db, handler, _) = Build();

        var ok = await handler.Handle(new UpdateRiskThresholdCommand
        {
            ThresholdId = Guid.NewGuid(),
            ThresholdValue = 5m,
            DefaultRiskLevel = RiskLevel.Warning,
            DefaultAlertLevel = AlertLevel.Level2_Managerial,
            UserId = "supervisor@dld.gov.ae"
        }, CancellationToken.None);

        Assert.False(ok);
        var reloaded = await db.RiskThresholds.AsNoTracking().SingleAsync();
        Assert.Null(reloaded.ModifiedBy); // nothing touched
    }

    [Fact]
    public async Task Can_demote_the_default_risk_and_alert_level_together()
    {
        var (db, handler, seeded) = Build();

        await handler.Handle(new UpdateRiskThresholdCommand
        {
            ThresholdId = seeded.Id,
            ThresholdValue = 18m,
            DefaultRiskLevel = RiskLevel.Warning,
            DefaultAlertLevel = AlertLevel.Level2_Managerial,
            UserId = "supervisor@dld.gov.ae"
        }, CancellationToken.None);

        var reloaded = await db.RiskThresholds.AsNoTracking().SingleAsync();
        Assert.Equal(RiskLevel.Warning, reloaded.DefaultRiskLevel);
        Assert.Equal(AlertLevel.Level2_Managerial, reloaded.DefaultAlertLevel);
        Assert.Equal(18m, reloaded.ThresholdValue);
    }

    [Fact]
    public async Task Writes_a_central_audit_row_with_before_and_after_values()
    {
        var (db, handler, seeded) = Build();

        await handler.Handle(new UpdateRiskThresholdCommand
        {
            ThresholdId = seeded.Id,
            ThresholdValue = 9m,
            DefaultRiskLevel = RiskLevel.High,
            DefaultAlertLevel = AlertLevel.Level3_SeniorLeadership,
            UserId = "supervisor@dld.gov.ae"
        }, CancellationToken.None);

        var audit = await db.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(nameof(RiskThreshold), audit.EntityType);
        Assert.Equal(seeded.Id.ToString(), audit.EntityId);
        Assert.Equal("Update", audit.Action);
        Assert.Equal("supervisor@dld.gov.ae", audit.UserId);
        Assert.Contains("12", audit.OldValues);
        Assert.Contains("9", audit.NewValues);
    }
}
