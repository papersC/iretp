using IRETP.Application.DTOs.Fabric;
using IRETP.Application.Interfaces;
using IRETP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IRETP.Infrastructure.Services.Fabric;

/// <summary>
/// Default implementation of <see cref="IFabricGoldDataSource"/>. Surfaces the
/// OneLake-style contract on top of the local OLTP store so the reference build
/// can advertise Fabric alignment (RFP v1.3 §11.4) without requiring a real
/// Fabric workspace. Production deployments swap this for a true OneLake or
/// semantic-model adapter and keep the same interface.
///
/// Watermarks and the published catalog are computed from the same EF context
/// the rest of the application uses, so freshness stays honest in lower
/// environments even though the data isn't physically in OneLake.
/// </summary>
public sealed class PassthroughFabricGoldDataSource : IFabricGoldDataSource
{
    // Allocated once per process. Adding a new Gold-layer model is a static
    // catalog change, not a runtime decision, so the array is shared safely
    // across every call to GetSemanticModelsAsync.
    private static readonly IReadOnlyList<FabricSemanticModelDto> GoldCatalog = new[]
    {
        new FabricSemanticModelDto(
            Name: "GoldTransactionFacts",
            Layer: "Gold",
            Description: "Cleansed, conformed transaction-grain fact table backing the public Transactions registry and the GIS heatmap.",
            Measures: new[] { "TransactionCount", "TotalValueAed", "AvgPricePerSqft" },
            Dimensions: new[] { "Zone", "PropertyType", "TransactionType", "DateKey" }),

        new FabricSemanticModelDto(
            Name: "GoldRentalYieldSemantic",
            Layer: "Gold",
            Description: "Quarterly rental-yield semantic model joining Ejari long-term contracts with sale comparables per zone.",
            Measures: new[] { "AvgGrossYield", "MedianRentAed", "YoYChangePct" },
            Dimensions: new[] { "Zone", "UnitType", "Quarter" }),

        new FabricSemanticModelDto(
            Name: "GoldDeveloperScorecard",
            Layer: "Gold",
            Description: "Composite developer score (six criteria) with full audit trail of weight history.",
            Measures: new[] { "CompositeScore", "OnTimeDeliveryPct", "EscrowAdequacyPct" },
            Dimensions: new[] { "Developer", "Quarter" }),

        new FabricSemanticModelDto(
            Name: "GoldEwrsAlertFact",
            Layer: "Gold",
            Description: "Risk-alert fact table feeding the EWRS dashboard and historical risk-trend charts.",
            Measures: new[] { "OpenAlertCount", "AvgTimeToAcknowledge", "EscalationRate" },
            Dimensions: new[] { "IndicatorType", "Severity", "Zone", "Developer", "DateKey" }),

        new FabricSemanticModelDto(
            Name: "GoldEscrowHealth",
            Layer: "Gold",
            Description: "Per-project escrow adequacy and balance-trend semantic model for the Escrow Monitoring dashboard.",
            Measures: new[] { "AdequacyRatio", "BalanceAed", "MonthOverMonthDelta" },
            Dimensions: new[] { "Project", "Developer", "Month" })
    };

    private readonly IretpDbContext _db;
    private readonly OneLakeFabricOptions _options;
    private readonly ILogger<PassthroughFabricGoldDataSource> _logger;

    public PassthroughFabricGoldDataSource(
        IretpDbContext db,
        IOptions<OneLakeFabricOptions> options,
        ILogger<PassthroughFabricGoldDataSource> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public FabricSourceMode Mode => _options.Mode == FabricSourceMode.Sql
        ? FabricSourceMode.PassthroughMirror
        : _options.Mode;

    public async Task<FabricHealthDto> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var available = false;
        string? detail = null;
        try
        {
            available = await _db.Database.CanConnectAsync(cancellationToken);
            if (!available) detail = "Database unreachable";
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            _logger.LogWarning(ex, "Fabric Gold passthrough probe failed");
        }

        return new FabricHealthDto(
            Mode,
            available,
            _options.WorkspaceId,
            _options.LakehouseId,
            _options.Region,
            detail,
            DateTime.UtcNow);
    }

    public Task<IReadOnlyList<FabricSemanticModelDto>> GetSemanticModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(GoldCatalog);

    public async Task<FabricFreshnessDto> GetFreshnessAsync(CancellationToken cancellationToken = default)
    {
        var goldWatermark = await _db.Transactions
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => (DateTime?)t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var silverWatermark = goldWatermark;

        TimeSpan? txnLag = goldWatermark.HasValue
            ? DateTime.UtcNow - goldWatermark.Value
            : null;

        // KPI lag mirrors the 15-min snapshot cache TTL when the most recent
        // watermark is within budget; otherwise reflect the actual gap.
        TimeSpan? kpiLag = txnLag is { } actual && actual < TimeSpan.FromMinutes(15)
            ? TimeSpan.FromMinutes(15)
            : txnLag;

        return new FabricFreshnessDto(
            GoldLayerLastWriteUtc: goldWatermark,
            SilverLayerLastWriteUtc: silverWatermark,
            TransactionLag: txnLag,
            KpiLag: kpiLag,
            LastPipelineRunId: $"passthrough-{DateTime.UtcNow:yyyyMMddHH}",
            LastPipelineStatus: txnLag.HasValue ? "Succeeded" : "NoData");
    }
}
