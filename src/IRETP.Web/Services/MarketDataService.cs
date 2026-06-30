using System.Globalization;

namespace IRETP.Web.Services;

// ===== DTOs =====

public record SalesTxnDto
{
    public DateTime TransactionDate { get; init; }
    public string PropertyType { get; init; } = "";
    public string AreaName { get; init; } = "";
    public string BuildingName { get; init; } = "";
    public int RoomCount { get; init; }
    public int PropertySizeSqft { get; init; }
    public decimal AmountAed { get; init; }
    public decimal PricePerSqft => PropertySizeSqft > 0 ? AmountAed / PropertySizeSqft : 0;
    public string TransactionType { get; init; } = "";  // Ready / Off-Plan
}

public record RentalTxnDto
{
    public DateTime RegistrationDate { get; init; }
    public string PropertyType { get; init; } = "";
    public string AreaName { get; init; } = "";
    public string BuildingName { get; init; } = "";
    public int RoomCount { get; init; }
    public int PropertySizeSqft { get; init; }
    public decimal AnnualRentAed { get; init; }
    public decimal RentPerSqft => PropertySizeSqft > 0 ? AnnualRentAed / PropertySizeSqft : 0;
    public string ContractType { get; init; } = "";   // New / Renewed
    public string PaymentFrequency { get; init; } = ""; // 1 / 2 / 4 / 12 cheques
}

public record MortgageTxnDto
{
    public DateTime TransactionDate { get; init; }
    public string PropertyType { get; init; } = "";
    public string AreaName { get; init; } = "";
    public string BuildingName { get; init; } = "";
    public int PropertySizeSqft { get; init; }
    public decimal AmountAed { get; init; }
    public string BankName { get; init; } = "";
    public string MortgageType { get; init; } = "";
    public int? LoanTermYears { get; init; }
    public decimal? InterestRate { get; init; }
}

public record PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record TxnFilter
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? SearchTerm { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? PropertyType { get; init; }
    public string? TransactionType { get; init; }
    public string SortBy { get; init; } = "TransactionDate";
    public bool SortDescending { get; init; } = true;
}

public record RankingRowDto
{
    public int Rank { get; init; }
    public string Name { get; init; } = "";
    public int TransactionCount { get; init; }
    public decimal TotalValue { get; init; }
    public decimal AvgPricePerSqft { get; init; }
    public string TopArea { get; init; } = "";
    public double MarketSharePercent { get; init; }
}

public record PriceIndexRowDto
{
    public string AreaName { get; init; } = "";
    public decimal AvgPricePerSqft { get; init; }
    public decimal YoyChangePercent { get; init; }
    public int SampleSize { get; init; }
}

/// <summary>Chart-ready monthly series — one bucket per calendar month.</summary>
public record MonthlySeriesDto
{
    public List<string> Labels { get; init; } = new();    // "Jan '25", "Feb '25", ...
    public List<int> Counts { get; init; } = new();       // Tx count per bucket
    public List<decimal> Values { get; init; } = new();   // Total AED per bucket
    public List<decimal> AvgPricePerSqft { get; init; } = new();
}

/// <summary>Single donut-ready bucket for transaction-type breakdown.</summary>
public record BreakdownSliceDto
{
    public string Label { get; init; } = "";
    public int Count { get; init; }
    public decimal Value { get; init; }
}

// ===== Service =====

/// <summary>
/// Seeded, in-memory mock market data covering sales/rentals/mortgages and
/// area/developer/broker rankings. Replace with repository-backed services
/// once the transactions pipeline is live.
/// </summary>
public class MarketDataService
{
    private static readonly string[] Areas =
    {
        "Dubai Marina","Downtown Dubai","Business Bay","Jumeirah Village Circle","Palm Jumeirah",
        "Dubai Hills Estate","Arabian Ranches","Jumeirah Lake Towers","Dubai Silicon Oasis","Dubai Sports City",
        "Mirdif","Al Barsha","Dubai Creek Harbour","Meydan","Dubai Investment Park"
    };
    private static readonly string[] PropertyTypes = { "Apartment","Villa","Townhouse","Land","Office","Retail" };
    private static readonly string[] Buildings =
    {
        "Marina Gate","Burj Vista","Executive Tower","Bellavista","Shoreline","Hartland Greens",
        "Creek Horizon","Address Residences","Emaar Beachfront","Meraas Boulevard","Silicon Heights","Liwa Heights"
    };
    private static readonly string[] Developers =
    {
        "Emaar Properties","DAMAC","Nakheel","Meraas","Dubai Properties","Sobha Realty",
        "Azizi Developments","Deyaar","Omniyat","Ellington Properties","Select Group","Binghatti"
    };
    private static readonly string[] Brokers =
    {
        "Allsopp & Allsopp","Betterhomes","haus & haus","Provident Estate","Luxhabitat","Driven Properties",
        "D&B Properties","fäm Properties","Espace Real Estate","White & Co","Metropolitan","Dacha"
    };
    private static readonly string[] Banks =
    {
        "Emirates NBD","ADCB","Mashreq","HSBC","Dubai Islamic Bank","ENBD Islamic","FAB","RAKBank","Standard Chartered"
    };

    private readonly List<SalesTxnDto> _sales;
    private readonly List<RentalTxnDto> _rentals;
    private readonly List<MortgageTxnDto> _mortgages;

    public MarketDataService()
    {
        var rng = new Random(42);
        _sales = GenerateSales(rng, 800);
        _rentals = GenerateRentals(rng, 600);
        _mortgages = GenerateMortgages(rng, 400);
    }

    private static List<SalesTxnDto> GenerateSales(Random rng, int count)
    {
        var list = new List<SalesTxnDto>(count);
        var now = new DateTime(2026, 3, 31);
        for (int i = 0; i < count; i++)
        {
            var pt = PropertyTypes[rng.Next(PropertyTypes.Length)];
            var size = pt switch
            {
                "Villa" => rng.Next(3500, 8000),
                "Townhouse" => rng.Next(1800, 3500),
                "Land" => rng.Next(5000, 20000),
                "Office" => rng.Next(800, 5000),
                "Retail" => rng.Next(400, 3000),
                _ => rng.Next(500, 2200)
            };
            var psf = rng.Next(800, 3600);
            list.Add(new SalesTxnDto
            {
                TransactionDate = now.AddDays(-rng.Next(0, 365 * 3)),
                PropertyType = pt,
                AreaName = Areas[rng.Next(Areas.Length)],
                BuildingName = Buildings[rng.Next(Buildings.Length)],
                RoomCount = pt == "Land" ? 0 : rng.Next(0, 6),
                PropertySizeSqft = size,
                AmountAed = size * psf,
                TransactionType = rng.NextDouble() < 0.58 ? "Off-Plan" : "Ready"
            });
        }
        return list;
    }

    private static List<RentalTxnDto> GenerateRentals(Random rng, int count)
    {
        var list = new List<RentalTxnDto>(count);
        var now = new DateTime(2026, 3, 31);
        var payFreq = new[] { "1 cheque","2 cheques","4 cheques","12 cheques" };
        for (int i = 0; i < count; i++)
        {
            var pt = new[] { "Apartment","Villa","Townhouse" }[rng.Next(3)];
            var size = pt == "Villa" ? rng.Next(3000, 7000) : pt == "Townhouse" ? rng.Next(1800, 3200) : rng.Next(500, 2000);
            var rentPsf = rng.Next(60, 220);
            list.Add(new RentalTxnDto
            {
                RegistrationDate = now.AddDays(-rng.Next(0, 365)),
                PropertyType = pt,
                AreaName = Areas[rng.Next(Areas.Length)],
                BuildingName = Buildings[rng.Next(Buildings.Length)],
                RoomCount = rng.Next(0, 6),
                PropertySizeSqft = size,
                AnnualRentAed = size * rentPsf,
                ContractType = rng.NextDouble() < 0.4 ? "Renewed" : "New",
                PaymentFrequency = payFreq[rng.Next(payFreq.Length)]
            });
        }
        return list;
    }

    private static List<MortgageTxnDto> GenerateMortgages(Random rng, int count)
    {
        var list = new List<MortgageTxnDto>(count);
        var now = new DateTime(2026, 3, 31);
        var mortgageTypes = new[] { "Purchase","Re-mortgage","Equity Release" };
        for (int i = 0; i < count; i++)
        {
            var pt = new[] { "Apartment","Villa","Townhouse" }[rng.Next(3)];
            var size = pt == "Villa" ? rng.Next(3000, 7000) : pt == "Townhouse" ? rng.Next(1800, 3200) : rng.Next(500, 2000);
            var psf = rng.Next(900, 3200);
            list.Add(new MortgageTxnDto
            {
                TransactionDate = now.AddDays(-rng.Next(0, 365 * 3)),
                PropertyType = pt,
                AreaName = Areas[rng.Next(Areas.Length)],
                BuildingName = Buildings[rng.Next(Buildings.Length)],
                PropertySizeSqft = size,
                AmountAed = (decimal)(size * psf * (0.65 + rng.NextDouble() * 0.2)),  // 65-85% LTV
                BankName = Banks[rng.Next(Banks.Length)],
                MortgageType = mortgageTypes[rng.Next(mortgageTypes.Length)],
                LoanTermYears = new[] { 15, 20, 25, 30 }[rng.Next(4)],
                InterestRate = Math.Round((decimal)(3.5 + rng.NextDouble() * 2.5), 2)
            });
        }
        return list;
    }

    // ---------- Query helpers ----------

    public Task<PagedResult<SalesTxnDto>> GetSalesAsync(TxnFilter f) =>
        Task.FromResult(Paginate(FilterSales(f), f, ApplySort(f)));

    public Task<PagedResult<RentalTxnDto>> GetRentalsAsync(TxnFilter f) =>
        Task.FromResult(Paginate(FilterRentals(f), f, null));

    public Task<PagedResult<MortgageTxnDto>> GetMortgagesAsync(TxnFilter f) =>
        Task.FromResult(Paginate(FilterMortgages(f), f, null));

    private IEnumerable<SalesTxnDto> FilterSales(TxnFilter f)
    {
        var q = _sales.AsEnumerable();
        if (f.DateFrom.HasValue) q = q.Where(x => x.TransactionDate >= f.DateFrom);
        if (f.DateTo.HasValue) q = q.Where(x => x.TransactionDate <= f.DateTo);
        if (!string.IsNullOrEmpty(f.PropertyType)) q = q.Where(x => x.PropertyType == f.PropertyType);
        if (!string.IsNullOrEmpty(f.TransactionType)) q = q.Where(x => x.TransactionType == f.TransactionType);
        if (!string.IsNullOrWhiteSpace(f.SearchTerm))
        {
            var t = f.SearchTerm.Trim();
            q = q.Where(x => x.AreaName.Contains(t, StringComparison.OrdinalIgnoreCase)
                           || x.BuildingName.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        return q;
    }

    private IEnumerable<RentalTxnDto> FilterRentals(TxnFilter f)
    {
        var q = _rentals.AsEnumerable();
        if (f.DateFrom.HasValue) q = q.Where(x => x.RegistrationDate >= f.DateFrom);
        if (f.DateTo.HasValue) q = q.Where(x => x.RegistrationDate <= f.DateTo);
        if (!string.IsNullOrEmpty(f.PropertyType)) q = q.Where(x => x.PropertyType == f.PropertyType);
        if (!string.IsNullOrWhiteSpace(f.SearchTerm))
        {
            var t = f.SearchTerm.Trim();
            q = q.Where(x => x.AreaName.Contains(t, StringComparison.OrdinalIgnoreCase)
                           || x.BuildingName.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        return q.OrderByDescending(x => x.RegistrationDate);
    }

    private IEnumerable<MortgageTxnDto> FilterMortgages(TxnFilter f)
    {
        var q = _mortgages.AsEnumerable();
        if (f.DateFrom.HasValue) q = q.Where(x => x.TransactionDate >= f.DateFrom);
        if (f.DateTo.HasValue) q = q.Where(x => x.TransactionDate <= f.DateTo);
        if (!string.IsNullOrEmpty(f.PropertyType)) q = q.Where(x => x.PropertyType == f.PropertyType);
        if (!string.IsNullOrWhiteSpace(f.SearchTerm))
        {
            var t = f.SearchTerm.Trim();
            q = q.Where(x => x.AreaName.Contains(t, StringComparison.OrdinalIgnoreCase)
                           || x.BuildingName.Contains(t, StringComparison.OrdinalIgnoreCase)
                           || x.BankName.Contains(t, StringComparison.OrdinalIgnoreCase));
        }
        return q.OrderByDescending(x => x.TransactionDate);
    }

    private static Func<IEnumerable<SalesTxnDto>, IEnumerable<SalesTxnDto>> ApplySort(TxnFilter f) => q =>
    {
        Func<SalesTxnDto, object> key = f.SortBy switch
        {
            "PropertyType" => x => x.PropertyType,
            "Area" => x => x.AreaName,
            "PropertySizeSqft" => x => x.PropertySizeSqft,
            "AmountAed" => x => x.AmountAed,
            _ => x => x.TransactionDate
        };
        return f.SortDescending ? q.OrderByDescending(key) : q.OrderBy(key);
    };

    private static PagedResult<T> Paginate<T>(IEnumerable<T> source, TxnFilter f,
        Func<IEnumerable<T>, IEnumerable<T>>? sorter)
    {
        var ordered = sorter == null ? source : sorter(source);
        var list = ordered.ToList();
        var page = ordered.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToList();
        return new PagedResult<T>
        {
            Items = page,
            TotalCount = list.Count,
            Page = f.Page,
            PageSize = f.PageSize
        };
    }

    // ---------- Rankings ----------

    public Task<List<RankingRowDto>> GetAreaRankingsAsync()
    {
        var grouped = _sales.GroupBy(x => x.AreaName).Select(g => new
        {
            Name = g.Key,
            Count = g.Count(),
            Total = g.Sum(x => x.AmountAed),
            AvgPsf = g.Average(x => x.PricePerSqft)
        }).OrderByDescending(x => x.Total).ToList();

        var grandTotal = grouped.Sum(x => x.Total);
        var ranked = grouped.Select((x, i) => new RankingRowDto
        {
            Rank = i + 1,
            Name = x.Name,
            TransactionCount = x.Count,
            TotalValue = x.Total,
            AvgPricePerSqft = Math.Round(x.AvgPsf, 0),
            MarketSharePercent = grandTotal == 0 ? 0 : Math.Round((double)(x.Total / grandTotal * 100), 1)
        }).ToList();
        return Task.FromResult(ranked);
    }

    public Task<List<RankingRowDto>> GetDeveloperRankingsAsync()
    {
        var rng = new Random(7);
        var total = _sales.Sum(x => x.AmountAed);
        // Simulate developer distribution: assign each developer a share of the market.
        var shares = Developers.Select(_ => rng.NextDouble()).ToArray();
        var sum = shares.Sum();
        var list = Developers.Select((name, i) =>
        {
            var share = shares[i] / sum;
            var tv = total * (decimal)share;
            return new { Name = name, Share = share, Total = tv };
        }).OrderByDescending(x => x.Total).ToList();

        var ranked = list.Select((x, i) => new RankingRowDto
        {
            Rank = i + 1,
            Name = x.Name,
            TransactionCount = (int)(_sales.Count * x.Share),
            TotalValue = x.Total,
            AvgPricePerSqft = 1200 + (decimal)(rng.NextDouble() * 1800),
            TopArea = Areas[rng.Next(Areas.Length)],
            MarketSharePercent = Math.Round(x.Share * 100, 1)
        }).ToList();
        return Task.FromResult(ranked);
    }

    public Task<List<RankingRowDto>> GetBrokerRankingsAsync()
    {
        var rng = new Random(13);
        var total = _sales.Sum(x => x.AmountAed);
        var shares = Brokers.Select(_ => rng.NextDouble()).ToArray();
        var sum = shares.Sum();
        var list = Brokers.Select((name, i) =>
        {
            var share = shares[i] / sum;
            var tv = total * (decimal)share;
            return new { Name = name, Share = share, Total = tv };
        }).OrderByDescending(x => x.Total).ToList();

        var ranked = list.Select((x, i) => new RankingRowDto
        {
            Rank = i + 1,
            Name = x.Name,
            TransactionCount = (int)(_sales.Count * x.Share),
            TotalValue = x.Total,
            AvgPricePerSqft = 1100 + (decimal)(rng.NextDouble() * 1900),
            TopArea = Areas[rng.Next(Areas.Length)],
            MarketSharePercent = Math.Round(x.Share * 100, 1)
        }).ToList();
        return Task.FromResult(ranked);
    }

    public Task<List<PriceIndexRowDto>> GetPriceIndexAsync()
    {
        var rng = new Random(21);
        var rows = _sales
            .GroupBy(x => x.AreaName)
            .Select(g => new PriceIndexRowDto
            {
                AreaName = g.Key,
                AvgPricePerSqft = Math.Round(g.Average(x => x.PricePerSqft), 0),
                SampleSize = g.Count(),
                YoyChangePercent = Math.Round((decimal)(rng.NextDouble() * 18 - 3), 1) // -3% to +15%
            })
            .OrderByDescending(x => x.AvgPricePerSqft)
            .ToList();
        return Task.FromResult(rows);
    }

    // ---------- FR-004 market-chart helpers ----------

    /// <summary>
    /// Monthly rollup of sales transactions between <paramref name="from"/>
    /// and <paramref name="to"/> inclusive — one bucket per calendar month.
    /// Feeds the dashboard's monthly-volume and price-trend charts (FR-004).
    /// </summary>
    public Task<MonthlySeriesDto> GetMonthlySalesSeriesAsync(DateTime from, DateTime to)
    {
        var buckets = MonthBuckets(from, to);
        var byMonth = _sales
            .Where(s => s.TransactionDate >= from && s.TransactionDate <= to)
            .GroupBy(s => new { s.TransactionDate.Year, s.TransactionDate.Month })
            .ToDictionary(g => (g.Key.Year, g.Key.Month));

        var series = new MonthlySeriesDto();
        foreach (var (year, month) in buckets)
        {
            series.Labels.Add($"{CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month)} '{year % 100:D2}");
            if (byMonth.TryGetValue((year, month), out var rows))
            {
                var list = rows.ToList();
                series.Counts.Add(list.Count);
                series.Values.Add(list.Sum(x => x.AmountAed));
                series.AvgPricePerSqft.Add(Math.Round(list.Average(x => x.PricePerSqft), 0));
            }
            else
            {
                series.Counts.Add(0);
                series.Values.Add(0);
                series.AvgPricePerSqft.Add(0);
            }
        }
        return Task.FromResult(series);
    }

    /// <summary>
    /// Ready vs. Off-Plan split for the donut-chart breakdown (FR-004).
    /// </summary>
    public Task<List<BreakdownSliceDto>> GetTransactionTypeBreakdownAsync(DateTime from, DateTime to)
    {
        var slices = _sales
            .Where(s => s.TransactionDate >= from && s.TransactionDate <= to)
            .GroupBy(s => s.TransactionType)
            .Select(g => new BreakdownSliceDto
            {
                Label = g.Key,
                Count = g.Count(),
                Value = g.Sum(x => x.AmountAed)
            })
            .OrderByDescending(s => s.Count)
            .ToList();
        return Task.FromResult(slices);
    }

    /// <summary>
    /// Top-N zones by transaction count for the bar chart (FR-004).
    /// </summary>
    public Task<List<RankingRowDto>> GetTopZonesByActivityAsync(DateTime from, DateTime to, int take = 5)
    {
        var rows = _sales
            .Where(s => s.TransactionDate >= from && s.TransactionDate <= to)
            .GroupBy(s => s.AreaName)
            .Select(g => new { Name = g.Key, Count = g.Count(), Total = g.Sum(x => x.AmountAed), Psf = g.Average(x => x.PricePerSqft) })
            .OrderByDescending(x => x.Count)
            .Take(take)
            .Select((x, i) => new RankingRowDto
            {
                Rank = i + 1,
                Name = x.Name,
                TransactionCount = x.Count,
                TotalValue = x.Total,
                AvgPricePerSqft = Math.Round(x.Psf, 0)
            })
            .ToList();
        return Task.FromResult(rows);
    }

    private static IEnumerable<(int Year, int Month)> MonthBuckets(DateTime from, DateTime to)
    {
        var cursor = new DateTime(from.Year, from.Month, 1);
        var end    = new DateTime(to.Year,   to.Month,   1);
        while (cursor <= end)
        {
            yield return (cursor.Year, cursor.Month);
            cursor = cursor.AddMonths(1);
        }
    }

    public static string FormatAed(decimal amount)
    {
        if (amount >= 1_000_000_000) return $"AED {amount / 1_000_000_000:N2}B";
        if (amount >= 1_000_000) return $"AED {amount / 1_000_000:N2}M";
        if (amount >= 1_000) return $"AED {amount / 1_000:N1}K";
        return $"AED {amount:N0}";
    }
}
