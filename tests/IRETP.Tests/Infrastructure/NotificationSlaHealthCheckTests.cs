using IRETP.Domain.Entities;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Repositories;
using IRETP.Infrastructure.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IRETP.Tests.Infrastructure;

/// <summary>
/// Covers the §6.2 delivery-SLA probe. Seeds fake Notification rows with
/// controlled (SentAt - CreatedAt) latencies and asserts the probe's
/// Healthy / Degraded / Unhealthy classification.
/// </summary>
public class NotificationSlaHealthCheckTests
{
    private static (IretpDbContext db, NotificationSlaHealthCheck check) Build()
    {
        var options = new DbContextOptionsBuilder<IretpDbContext>()
            .UseInMemoryDatabase(databaseName: $"notif-sla-{Guid.NewGuid():N}")
            .Options;
        var db = new IretpDbContext(options);
        return (db, new NotificationSlaHealthCheck(new Repository<Notification>(db)));
    }

    private static void Seed(IretpDbContext db, string channel, int count, TimeSpan latency)
    {
        // IretpDbContext.SaveChangesAsync unconditionally stamps CreatedAt =
        // UtcNow on Added rows, so we set SentAt = UtcNow + latency and let
        // CreatedAt be overwritten — (SentAt - CreatedAt) then equals latency
        // to within a few ms, which is fine for p95 classification.
        var now = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = $"u{i}",
                Title = "t", TitleAr = "ت", Message = "m", MessageAr = "م",
                Channel = channel,
                IsSent = true,
                SentAt = now.Add(latency)
            });
        }
        db.SaveChanges();
    }

    [Fact]
    public async Task Healthy_when_no_samples_in_window()
    {
        var (_, check) = Build();
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Healthy_when_email_p95_under_five_minute_budget()
    {
        var (db, check) = Build();
        Seed(db, "Email", 20, TimeSpan.FromMinutes(2));

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Degraded_when_email_p95_between_budget_and_double()
    {
        var (db, check) = Build();
        Seed(db, "Email", 20, TimeSpan.FromMinutes(6));

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task Unhealthy_when_sms_p95_exceeds_double_budget()
    {
        var (db, check) = Build();
        // 3-min SMS budget — latencies of 7 min are past the 2× breach line.
        Seed(db, "SMS", 20, TimeSpan.FromMinutes(7));

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Mixed_channels_only_fail_on_the_breaching_one()
    {
        var (db, check) = Build();
        Seed(db, "Email", 20, TimeSpan.FromMinutes(2));       // healthy
        Seed(db, "InPlatform", 20, TimeSpan.FromMinutes(5)); // 5-min >> 30s → past 2× budget, Unhealthy

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("InPlatform", result.Description!);
    }
}
