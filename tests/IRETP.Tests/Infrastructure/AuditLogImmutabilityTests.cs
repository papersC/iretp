using IRETP.Domain.Entities;
using IRETP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Tests.Infrastructure;

/// <summary>
/// RFP §10.2 — the AuditLog table is append-only. Verifies the DbContext
/// interceptor rejects UPDATE and DELETE while permitting INSERT.
/// </summary>
public class AuditLogImmutabilityTests
{
    private static IretpDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<IretpDbContext>()
            .UseInMemoryDatabase(databaseName: $"audit-immut-{Guid.NewGuid():N}")
            .Options;
        return new IretpDbContext(options);
    }

    private static AuditLog SampleAudit() => new()
    {
        Id = Guid.NewGuid(),
        EntityType = "RiskThreshold",
        EntityId = Guid.NewGuid().ToString(),
        Action = "Update",
        UserId = "alice",
        UserName = "alice@dld.gov.ae",
        OldValues = "{\"Weight\":25}",
        NewValues = "{\"Weight\":30}"
    };

    [Fact]
    public async Task Insert_is_allowed()
    {
        using var db = NewDb();
        db.AuditLogs.Add(SampleAudit());
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task Update_is_rejected()
    {
        using var db = NewDb();
        var row = SampleAudit();
        db.AuditLogs.Add(row);
        await db.SaveChangesAsync();

        // Retrieve via a tracked read so the change tracker sees a modification.
        var tracked = await db.AuditLogs.SingleAsync();
        tracked.Action = "Tamper";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Delete_is_rejected()
    {
        using var db = NewDb();
        var row = SampleAudit();
        db.AuditLogs.Add(row);
        await db.SaveChangesAsync();

        var tracked = await db.AuditLogs.SingleAsync();
        db.AuditLogs.Remove(tracked);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("immutable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
