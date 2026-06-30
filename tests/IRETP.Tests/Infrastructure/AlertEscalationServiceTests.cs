using IRETP.Domain.Common;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Repositories;
using IRETP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace IRETP.Tests.Infrastructure;

/// <summary>
/// Auto-escalation sweep (RFP §8.2). Verifies that only breached
/// <c>AlertStatus.New</c> alerts are promoted, that Level-4 never escalates
/// further, that deadlines are rewritten from the new level's SLA, and that
/// <c>AutoEscalated</c> + <c>LastEscalatedAt</c> are stamped.
/// </summary>
public class AlertEscalationServiceTests
{
    private readonly string _dbName = $"escalation-{Guid.NewGuid():N}";

    private (IServiceProvider sp, IretpDbContext db) BuildServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<IretpDbContext>(o => o.UseInMemoryDatabase(_dbName), ServiceLifetime.Scoped);
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<AlertEscalationService>(p =>
            new AlertEscalationService(p.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<AlertEscalationService>.Instance));
        var sp = services.BuildServiceProvider();
        var db = sp.CreateScope().ServiceProvider.GetRequiredService<IretpDbContext>();
        return (sp, db);
    }

    private static async Task<RiskAlert> ReloadNoTrackingAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IretpDbContext>()
            .RiskAlerts.AsNoTracking().SingleAsync();
    }

    private static RiskAlert AlertAt(AlertLevel level, AlertStatus status, DateTime? ackDeadline)
        => new()
        {
            Id = Guid.NewGuid(),
            IndicatorType = "EscrowShortfall_Warning",
            RiskLevel = RiskLevel.Warning,
            AlertLevel = level,
            Status = status,
            Title = "t", Description = "d",
            AcknowledgeDeadline = ackDeadline
        };

    [Fact]
    public async Task Breached_level_one_escalates_to_level_two()
    {
        var (sp, db) = BuildServices();
        var alert = AlertAt(AlertLevel.Level1_Operational, AlertStatus.New,
            DateTime.UtcNow.AddHours(-1));
        db.RiskAlerts.Add(alert);
        await db.SaveChangesAsync();

        // Force a fresh read through a new scope — the escalation service
        // opens its own DbContext, so the seeded state must be visible there.
        using var probe = sp.CreateScope();
        var seen = await probe.ServiceProvider.GetRequiredService<IretpDbContext>()
            .RiskAlerts.CountAsync();
        Assert.True(seen >= 1, $"Seeded alert not visible to service scope — {seen} rows");

        await sp.GetRequiredService<AlertEscalationService>().EscalateBreachedAlertsAsync();

        // Fetch via yet another scope so we don't get the in-memory tracked copy.
        using var verify = sp.CreateScope();
        var reloaded = await verify.ServiceProvider.GetRequiredService<IretpDbContext>()
            .RiskAlerts.AsNoTracking().SingleAsync();
        Assert.Equal(AlertLevel.Level2_Managerial, reloaded.AlertLevel);
        Assert.True(reloaded.AutoEscalated);
        Assert.NotNull(reloaded.LastEscalatedAt);
        Assert.Equal(AlertStatus.New, reloaded.Status); // reset so dispatcher re-fans-out
        Assert.Contains("Auto-escalated Level1_Operational", reloaded.EscalationPath);
    }

    [Fact]
    public async Task Already_acknowledged_alerts_do_not_escalate()
    {
        var (sp, db) = BuildServices();
        db.RiskAlerts.Add(AlertAt(AlertLevel.Level1_Operational, AlertStatus.Acknowledged,
            DateTime.UtcNow.AddHours(-2)));
        await db.SaveChangesAsync();

        await sp.GetRequiredService<AlertEscalationService>().EscalateBreachedAlertsAsync();

        var reloaded = await ReloadNoTrackingAsync(sp);
        Assert.Equal(AlertLevel.Level1_Operational, reloaded.AlertLevel);
        Assert.False(reloaded.AutoEscalated);
    }

    [Fact]
    public async Task Alerts_within_deadline_do_not_escalate()
    {
        var (sp, db) = BuildServices();
        db.RiskAlerts.Add(AlertAt(AlertLevel.Level1_Operational, AlertStatus.New,
            DateTime.UtcNow.AddHours(1)));
        await db.SaveChangesAsync();

        await sp.GetRequiredService<AlertEscalationService>().EscalateBreachedAlertsAsync();

        var reloaded = await ReloadNoTrackingAsync(sp);
        Assert.Equal(AlertLevel.Level1_Operational, reloaded.AlertLevel);
        Assert.False(reloaded.AutoEscalated);
    }

    [Fact]
    public async Task Level_four_never_escalates_further()
    {
        var (sp, db) = BuildServices();
        db.RiskAlerts.Add(AlertAt(AlertLevel.Level4_Strategic, AlertStatus.New,
            DateTime.UtcNow.AddDays(-1)));
        await db.SaveChangesAsync();

        await sp.GetRequiredService<AlertEscalationService>().EscalateBreachedAlertsAsync();

        var reloaded = await ReloadNoTrackingAsync(sp);
        Assert.Equal(AlertLevel.Level4_Strategic, reloaded.AlertLevel);
        Assert.False(reloaded.AutoEscalated);
    }

    [Fact]
    public async Task Deadlines_are_rewritten_from_new_level_SLA()
    {
        var (sp, db) = BuildServices();
        var originalDeadline = DateTime.UtcNow.AddHours(-2);
        db.RiskAlerts.Add(AlertAt(AlertLevel.Level1_Operational, AlertStatus.New, originalDeadline));
        await db.SaveChangesAsync();

        await sp.GetRequiredService<AlertEscalationService>().EscalateBreachedAlertsAsync();

        var reloaded = await ReloadNoTrackingAsync(sp);
        Assert.NotNull(reloaded.AcknowledgeDeadline);
        // Level 2 gets 2-hour ack window — the new deadline must be later than
        // the original Level-1 deadline.
        Assert.True(reloaded.AcknowledgeDeadline > originalDeadline);
    }
}
