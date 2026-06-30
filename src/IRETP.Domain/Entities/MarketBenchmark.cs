using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

/// <summary>
/// Quarterly market snapshot for a global peer city — backs the
/// International Market Benchmarking module (RFP Section 20). Data is sourced
/// from publicly-available index publishers (JLL, Savills, Knight Frank) and
/// refreshed quarterly by the DLD research team.
/// </summary>
public class MarketBenchmark : BaseEntity
{
    public string CityCode { get; set; } = default!;    // DXB, LON, SGP, NYC, PAR, HKG
    public string CityName { get; set; } = default!;
    public string CountryCode { get; set; } = default!;

    public int Year { get; set; }
    public int Quarter { get; set; }

    /// <summary>JLL GRETI composite (lower = more transparent).</summary>
    public decimal GretiCompositeScore { get; set; }

    public decimal AveragePricePerSqft { get; set; }     // USD
    public decimal AverageGrossRentalYieldPct { get; set; }
    public decimal PrimePriceYoYPct { get; set; }
    public decimal TransactionVolumeYoYPct { get; set; }

    /// <summary>Share of institutional / foreign direct investment.</summary>
    public decimal InstitutionalCapitalSharePct { get; set; }

    public string? Notes { get; set; }
}
