using IRETP.Application.DTOs.Fabric;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Services.Fabric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace IRETP.Tests.Infrastructure;

/// <summary>
/// Covers the OneLake/Fabric adapter contract (RFP v1.3 §11.4). The passthrough
/// implementation must honour the configured mode, expose the published Gold
/// semantic-model catalog, and surface a freshness watermark derived from the
/// most recent transaction row.
/// </summary>
public class PassthroughFabricGoldDataSourceTests
{
    private static PassthroughFabricGoldDataSource Build(IretpDbContext db, OneLakeFabricOptions options)
    {
        return new PassthroughFabricGoldDataSource(
            db,
            Options.Create(options),
            NullLogger<PassthroughFabricGoldDataSource>.Instance);
    }

    private static IretpDbContext NewContext()
    {
        var dbOptions = new DbContextOptionsBuilder<IretpDbContext>()
            .UseInMemoryDatabase($"fabric-{Guid.NewGuid():N}")
            .Options;
        return new IretpDbContext(dbOptions);
    }

    [Fact]
    public void Mode_falls_back_to_PassthroughMirror_when_configured_as_Sql()
    {
        using var db = NewContext();
        var sut = Build(db, new OneLakeFabricOptions { Mode = FabricSourceMode.Sql });
        Assert.Equal(FabricSourceMode.PassthroughMirror, sut.Mode);
    }

    [Fact]
    public void Mode_preserves_explicit_OneLakeDirect_configuration()
    {
        using var db = NewContext();
        var sut = Build(db, new OneLakeFabricOptions { Mode = FabricSourceMode.OneLakeDirect });
        Assert.Equal(FabricSourceMode.OneLakeDirect, sut.Mode);
    }

    [Fact]
    public async Task Probe_returns_available_when_in_memory_database_reachable()
    {
        using var db = NewContext();
        var sut = Build(db, new OneLakeFabricOptions
        {
            Mode = FabricSourceMode.PassthroughMirror,
            WorkspaceId = "ws-1",
            LakehouseId = "lh-1",
            Region = "UAE North"
        });

        var health = await sut.ProbeAsync();

        Assert.True(health.Available);
        Assert.Equal("ws-1", health.WorkspaceId);
        Assert.Equal("lh-1", health.LakehouseId);
        Assert.Equal("UAE North", health.Region);
        Assert.Equal(FabricSourceMode.PassthroughMirror, health.Mode);
    }

    [Fact]
    public async Task Semantic_models_include_core_gold_layer_facts()
    {
        using var db = NewContext();
        var sut = Build(db, new OneLakeFabricOptions { Mode = FabricSourceMode.PassthroughMirror });

        var models = await sut.GetSemanticModelsAsync();

        // The five core Gold-layer models documented in the Architecture
        // Integration Map must be discoverable from the admin endpoint so
        // DLD can confirm what the platform reads from Fabric.
        Assert.Contains(models, m => m.Name == "GoldTransactionFacts");
        Assert.Contains(models, m => m.Name == "GoldRentalYieldSemantic");
        Assert.Contains(models, m => m.Name == "GoldDeveloperScorecard");
        Assert.Contains(models, m => m.Name == "GoldEwrsAlertFact");
        Assert.Contains(models, m => m.Name == "GoldEscrowHealth");
        Assert.All(models, m => Assert.Equal("Gold", m.Layer));
    }

    [Fact]
    public async Task Freshness_returns_null_watermark_when_no_transactions_exist()
    {
        using var db = NewContext();
        var sut = Build(db, new OneLakeFabricOptions { Mode = FabricSourceMode.PassthroughMirror });

        var freshness = await sut.GetFreshnessAsync();

        Assert.Null(freshness.GoldLayerLastWriteUtc);
        Assert.Null(freshness.TransactionLag);
        Assert.Equal("NoData", freshness.LastPipelineStatus);
    }

    [Fact]
    public async Task Freshness_reports_kpi_lag_within_15_minute_budget_when_watermark_is_recent()
    {
        using var db = NewContext();
        var zone = new Zone { Id = Guid.NewGuid(), Name = "Test", NameAr = "اختبار" };
        db.Zones.Add(zone);
        db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            ZoneId = zone.Id,
            TransactionDate = DateTime.UtcNow,
            PropertyType = PropertyType.Apartment,
            TransactionType = TransactionType.Sale,
            AreaSqft = 1000m,
            AreaSqm = 92.9m,
            TransactionValue = 1_500_000m,
            PricePerSqft = 1500m,
            FinancingMethod = FinancingMethod.Cash
        });
        await db.SaveChangesAsync();

        var sut = Build(db, new OneLakeFabricOptions { Mode = FabricSourceMode.PassthroughMirror });
        var freshness = await sut.GetFreshnessAsync();

        Assert.NotNull(freshness.GoldLayerLastWriteUtc);
        Assert.True(freshness.TransactionLag < TimeSpan.FromMinutes(15));
        Assert.Equal(TimeSpan.FromMinutes(15), freshness.KpiLag);
        Assert.Equal("Succeeded", freshness.LastPipelineStatus);
    }
}
