using IRETP.Application.Features.DeveloperRating.Commands;
using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Repositories;
using IRETP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Tests.Application;

/// <summary>
/// Covers the RFP §9.1.2 weight-configuration invariants:
/// <list type="bullet">
///   <item>Weights must always sum to 100%.</item>
///   <item>Every change stamps ModifiedBy + ModifiedAt on the criterion row.</item>
///   <item>Unknown criterion keys are rejected rather than silently dropped.</item>
/// </list>
/// </summary>
public class UpdateScoringWeightsCommandHandlerTests
{
    // RFP §9.1.1 default weights — mirrored here so the test is self-contained.
    private static readonly (string Key, decimal Weight)[] DefaultWeights =
    {
        ("OnTimeDelivery",          25m),
        ("UnitSalesCompletion",     20m),
        ("EscrowHealth",            20m),
        ("RegulatoryCompliance",    15m),
        ("FinancialSoundness",      10m),
        ("HistoricalSuccess",       10m)
    };

    private static (IretpDbContext db, UpdateScoringWeightsCommandHandler handler, IAuditLogService audit) Build()
    {
        var options = new DbContextOptionsBuilder<IretpDbContext>()
            .UseInMemoryDatabase(databaseName: $"weights-{Guid.NewGuid():N}")
            .Options;
        var db = new IretpDbContext(options);
        foreach (var (key, weight) in DefaultWeights)
        {
            db.ScoringWeights.Add(new ScoringWeight
            {
                Id = Guid.NewGuid(),
                CriterionKey = key,
                CriterionName = key,
                CriterionNameAr = key,
                Weight = weight
            });
        }
        db.SaveChanges();

        var repo = new Repository<ScoringWeight>(db);
        var uow = new UnitOfWork(db);
        var audit = new AuditLogService(new Repository<AuditLog>(db), uow);
        return (db, new UpdateScoringWeightsCommandHandler(repo, uow, audit), audit);
    }

    private static UpdateScoringWeightsCommand CmdSumming100 => new()
    {
        ModifiedBy = "alice@dld.gov.ae",
        Weights =
        [
            new() { CriterionKey = "OnTimeDelivery",       Weight = 30m },
            new() { CriterionKey = "UnitSalesCompletion",  Weight = 20m },
            new() { CriterionKey = "EscrowHealth",         Weight = 20m },
            new() { CriterionKey = "RegulatoryCompliance", Weight = 15m },
            new() { CriterionKey = "FinancialSoundness",   Weight = 10m },
            new() { CriterionKey = "HistoricalSuccess",    Weight = 5m }
        ]
    };

    [Fact]
    public async Task Accepts_weights_that_sum_to_exactly_one_hundred()
    {
        var (db, handler, _) = Build();
        var ok = await handler.Handle(CmdSumming100, CancellationToken.None);

        Assert.True(ok);
        var refreshed = await db.ScoringWeights.ToDictionaryAsync(w => w.CriterionKey);
        Assert.Equal(30m, refreshed["OnTimeDelivery"].Weight);
        Assert.Equal(5m,  refreshed["HistoricalSuccess"].Weight);
        Assert.Equal(100m, refreshed.Values.Sum(w => w.Weight));
    }

    [Fact]
    public async Task Rejects_weights_that_total_less_than_one_hundred()
    {
        var (_, handler, _) = Build();
        var cmd = CmdSumming100;
        cmd.Weights[^1].Weight = 4m; // total = 99

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("100%", ex.Message);
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public async Task Rejects_weights_that_total_more_than_one_hundred()
    {
        var (_, handler, _) = Build();
        var cmd = CmdSumming100;
        cmd.Weights[0].Weight = 45m; // total = 115

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("100%", ex.Message);
    }

    [Fact]
    public async Task Rejects_unknown_criterion_key()
    {
        var (_, handler, _) = Build();
        var cmd = CmdSumming100;
        cmd.Weights[^1].CriterionKey = "NonExistent";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(cmd, CancellationToken.None));
        Assert.Contains("NonExistent", ex.Message);
    }

    [Fact]
    public async Task Stamps_ModifiedBy_and_ModifiedAt_on_every_changed_row()
    {
        var (db, handler, _) = Build();
        var startedAt = DateTime.UtcNow.AddSeconds(-1);

        await handler.Handle(CmdSumming100, CancellationToken.None);

        var rows = await db.ScoringWeights.ToListAsync();
        foreach (var row in rows)
        {
            Assert.Equal("alice@dld.gov.ae", row.ModifiedBy);
            Assert.NotNull(row.ModifiedAt);
            Assert.InRange(row.ModifiedAt!.Value, startedAt, DateTime.UtcNow.AddSeconds(1));
        }
    }

    [Fact]
    public async Task Writes_a_central_audit_row_for_the_weight_change()
    {
        var (db, handler, _) = Build();

        await handler.Handle(CmdSumming100, CancellationToken.None);

        var audit = await db.AuditLogs.AsNoTracking().SingleAsync();
        Assert.Equal(nameof(ScoringWeight), audit.EntityType);
        Assert.Equal("Update", audit.Action);
        Assert.Equal("alice@dld.gov.ae", audit.UserId);
        Assert.Contains("OnTimeDelivery", audit.OldValues);
        Assert.Contains("OnTimeDelivery", audit.NewValues);
        Assert.Contains("25", audit.OldValues);
        Assert.Contains("30", audit.NewValues);
    }

    [Fact]
    public async Task Rejected_command_does_not_write_audit_row()
    {
        var (db, handler, _) = Build();
        var cmd = CmdSumming100;
        cmd.Weights[0].Weight = 60m; // total = 130

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(cmd, CancellationToken.None));

        Assert.Equal(0, await db.AuditLogs.CountAsync());
    }
}
