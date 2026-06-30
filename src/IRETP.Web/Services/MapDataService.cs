namespace IRETP.Web.Services;

public record AreaMapDataDto
{
    public int AreaId { get; init; }
    public string Name { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int TransactionCount { get; init; }
    public decimal TotalValue { get; init; }
    public decimal AvgPricePerSqft { get; init; }
    /// <summary>12-month volume trend used for sparklines (normalized 0..1).</summary>
    public double[] VolumeTrend { get; init; } = Array.Empty<double>();
    public int OffPlanPercent { get; init; } = 58;
    /// <summary>Gross rental yield % for the zone (heatmap layer 3).</summary>
    public decimal RentalYield { get; init; } = 6.5m;
    /// <summary>Certified-green-building coverage % for the zone (RFP §20 ESG heatmap layer).</summary>
    public decimal EsgCoveragePct { get; init; } = 0m;
    /// <summary>Average certification level (1=Entry .. 5=Exemplary) among certified projects in the zone.</summary>
    public decimal EsgAverageLevel { get; init; } = 0m;
}

public record DubaiAveragesDto
{
    public decimal AvgPricePerSqft { get; init; }
    public double AvgTransactionCount { get; init; }
    public decimal AvgTotalValue { get; init; }
}

public record MapKpiSummaryDto
{
    public decimal TotalSalesVolume { get; init; }
    public int TotalSalesTransactions { get; init; }
    public decimal AveragePricePerSqft { get; init; }
    public string TopRegionByValue { get; init; } = "";
    public DateTime DataAsOf { get; init; }
}

/// <summary>
/// Individual project pin used by the FR-011 clustered layer on /map.
/// Mock coordinates are scattered around the zone centroid.
/// </summary>
public record ProjectPinDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Developer { get; init; } = "";
    public string Zone { get; init; } = "";
    /// <summary>Completed / UnderConstruction / FutureAnnounced / Stalled.</summary>
    public string Status { get; init; } = "";
    public int CompletionPercent { get; init; }
    public int TotalUnits { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}

/// <summary>
/// Mock data source for the interactive map page. Replace with a repository-backed
/// implementation when the transactions pipeline is wired up.
/// </summary>
public class MapDataService
{
    public Task<MapKpiSummaryDto> GetKpiSummaryAsync() =>
        Task.FromResult(new MapKpiSummaryDto
        {
            TotalSalesVolume = 761_400_000_000m,
            TotalSalesTransactions = 226_531,
            AveragePricePerSqft = 1_842m,
            TopRegionByValue = "Dubai Marina",
            DataAsOf = new DateTime(2026, 3, 31)
        });

    public Task<List<AreaMapDataDto>> GetAreaMapDataAsync()
    {
        var seed = new Random(101);
        double[] Trend() => Enumerable.Range(0, 12).Select(i =>
            Math.Max(0.15, 0.4 + 0.55 * seed.NextDouble() + i * 0.02)).ToArray();

        return Task.FromResult(new List<AreaMapDataDto>
        {
            new() { AreaId = 1,  Name = "Dubai Marina",                Latitude = 25.0805, Longitude = 55.1403, TransactionCount = 1243, TotalValue = 9_850_000_000m,  AvgPricePerSqft = 2_650m, VolumeTrend = Trend(), OffPlanPercent = 58, RentalYield = 5.8m, EsgCoveragePct = 46.2m, EsgAverageLevel = 3.4m },
            new() { AreaId = 2,  Name = "Downtown Dubai",              Latitude = 25.1972, Longitude = 55.2744, TransactionCount = 982,  TotalValue = 12_300_000_000m, AvgPricePerSqft = 3_120m, VolumeTrend = Trend(), OffPlanPercent = 42, RentalYield = 5.1m, EsgCoveragePct = 72.8m, EsgAverageLevel = 4.1m },
            new() { AreaId = 3,  Name = "Business Bay",                Latitude = 25.1857, Longitude = 55.2650, TransactionCount = 1456, TotalValue = 7_200_000_000m,  AvgPricePerSqft = 1_980m, VolumeTrend = Trend(), OffPlanPercent = 64, RentalYield = 6.4m, EsgCoveragePct = 38.5m, EsgAverageLevel = 3.0m },
            new() { AreaId = 4,  Name = "Jumeirah Village Circle",     Latitude = 25.0580, Longitude = 55.2080, TransactionCount = 1987, TotalValue = 3_800_000_000m,  AvgPricePerSqft = 1_120m, VolumeTrend = Trend(), OffPlanPercent = 72, RentalYield = 8.2m, EsgCoveragePct = 12.4m, EsgAverageLevel = 2.1m },
            new() { AreaId = 5,  Name = "Palm Jumeirah",               Latitude = 25.1124, Longitude = 55.1390, TransactionCount = 543,  TotalValue = 11_100_000_000m, AvgPricePerSqft = 3_480m, VolumeTrend = Trend(), OffPlanPercent = 28, RentalYield = 4.6m, EsgCoveragePct = 61.7m, EsgAverageLevel = 3.8m },
            new() { AreaId = 6,  Name = "Dubai Hills Estate",          Latitude = 25.1037, Longitude = 55.2400, TransactionCount = 734,  TotalValue = 5_400_000_000m,  AvgPricePerSqft = 1_740m, VolumeTrend = Trend(), OffPlanPercent = 68, RentalYield = 6.1m, EsgCoveragePct = 81.3m, EsgAverageLevel = 4.2m },
            new() { AreaId = 7,  Name = "Arabian Ranches",             Latitude = 25.0511, Longitude = 55.2693, TransactionCount = 312,  TotalValue = 2_100_000_000m,  AvgPricePerSqft = 1_260m, VolumeTrend = Trend(), OffPlanPercent = 18, RentalYield = 5.5m, EsgCoveragePct = 24.8m, EsgAverageLevel = 2.6m },
            new() { AreaId = 8,  Name = "Jumeirah Lake Towers",        Latitude = 25.0693, Longitude = 55.1376, TransactionCount = 891,  TotalValue = 4_200_000_000m,  AvgPricePerSqft = 1_520m, VolumeTrend = Trend(), OffPlanPercent = 36, RentalYield = 7.0m, EsgCoveragePct = 29.1m, EsgAverageLevel = 2.8m },
            new() { AreaId = 9,  Name = "Dubai Silicon Oasis",         Latitude = 25.1279, Longitude = 55.3870, TransactionCount = 645,  TotalValue = 1_800_000_000m,  AvgPricePerSqft = 980m,   VolumeTrend = Trend(), OffPlanPercent = 48, RentalYield = 8.6m, EsgCoveragePct = 18.9m, EsgAverageLevel = 2.3m },
            new() { AreaId = 10, Name = "Dubai Sports City",           Latitude = 25.0383, Longitude = 55.2260, TransactionCount = 512,  TotalValue = 1_500_000_000m,  AvgPricePerSqft = 920m,   VolumeTrend = Trend(), OffPlanPercent = 55, RentalYield = 8.9m, EsgCoveragePct = 14.5m, EsgAverageLevel = 2.0m },
            new() { AreaId = 11, Name = "Mirdif",                      Latitude = 25.2188, Longitude = 55.4200, TransactionCount = 287,  TotalValue = 1_200_000_000m,  AvgPricePerSqft = 1_080m, VolumeTrend = Trend(), OffPlanPercent = 12, RentalYield = 6.8m, EsgCoveragePct = 8.2m,  EsgAverageLevel = 1.8m },
            new() { AreaId = 12, Name = "Al Barsha",                   Latitude = 25.1107, Longitude = 55.2070, TransactionCount = 423,  TotalValue = 2_500_000_000m,  AvgPricePerSqft = 1_380m, VolumeTrend = Trend(), OffPlanPercent = 32, RentalYield = 6.9m, EsgCoveragePct = 22.6m, EsgAverageLevel = 2.4m },
            new() { AreaId = 13, Name = "Dubai Creek Harbour",         Latitude = 25.1970, Longitude = 55.3430, TransactionCount = 678,  TotalValue = 6_700_000_000m,  AvgPricePerSqft = 2_240m, VolumeTrend = Trend(), OffPlanPercent = 78, RentalYield = 5.3m, EsgCoveragePct = 68.4m, EsgAverageLevel = 3.9m },
            new() { AreaId = 14, Name = "Meydan",                      Latitude = 25.1587, Longitude = 55.3028, TransactionCount = 389,  TotalValue = 3_100_000_000m,  AvgPricePerSqft = 1_620m, VolumeTrend = Trend(), OffPlanPercent = 52, RentalYield = 6.6m, EsgCoveragePct = 41.7m, EsgAverageLevel = 3.1m },
            new() { AreaId = 15, Name = "Dubai Investment Park",       Latitude = 24.9871, Longitude = 55.1733, TransactionCount = 201,  TotalValue = 900_000_000m,    AvgPricePerSqft = 780m,   VolumeTrend = Trend(), OffPlanPercent = 22, RentalYield = 9.4m, EsgCoveragePct = 5.1m,  EsgAverageLevel = 1.5m },
        });
    }

    /// <summary>
    /// Individual project pins scattered across the map — feeds the
    /// FR-011 clustered projects layer. Status distribution mirrors a
    /// realistic Dubai mix (≈50% Under Construction, 25% Completed,
    /// 20% Future/Announced, 5% Stalled).
    /// </summary>
    public async Task<List<ProjectPinDto>> GetProjectPinsAsync()
    {
        var areas = await GetAreaMapDataAsync();
        var rng = new Random(3110);
        var statuses = new[]
        {
            ("UnderConstruction", 0.50),
            ("Completed",         0.25),
            ("FutureAnnounced",   0.20),
            ("Stalled",           0.05)
        };
        var developers = new[]
        {
            "Emaar Properties","DAMAC","Nakheel","Meraas","Dubai Properties","Sobha Realty",
            "Azizi Developments","Deyaar","Omniyat","Ellington","Select Group","Binghatti"
        };
        var projectWords = new[] { "Towers","Residences","Heights","Park","Square","Gardens","Plaza","Boulevard","Bay","Views","Marina","Vista" };

        var pins = new List<ProjectPinDto>();
        foreach (var area in areas)
        {
            // Bigger zones get more projects — scale with transaction volume.
            var count = Math.Clamp(area.TransactionCount / 60, 6, 28);
            for (var i = 0; i < count; i++)
            {
                // ~2km scatter around the zone centroid (0.018° ~ 2km)
                var latJitter = (rng.NextDouble() - 0.5) * 0.036;
                var lonJitter = (rng.NextDouble() - 0.5) * 0.036;

                var roll = rng.NextDouble();
                var cum = 0.0;
                var status = "UnderConstruction";
                foreach (var (s, w) in statuses)
                {
                    cum += w;
                    if (roll <= cum) { status = s; break; }
                }
                var completion = status switch
                {
                    "Completed"       => 100,
                    "FutureAnnounced" => rng.Next(0, 5),
                    "Stalled"         => rng.Next(20, 70),
                    _                 => rng.Next(20, 95)
                };

                pins.Add(new ProjectPinDto
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Name = $"{area.Name.Split(' ')[0]} {projectWords[rng.Next(projectWords.Length)]} {(char)('A' + rng.Next(6))}{rng.Next(1, 9)}",
                    Developer = developers[rng.Next(developers.Length)],
                    Zone = area.Name,
                    Status = status,
                    CompletionPercent = completion,
                    TotalUnits = 40 + rng.Next(260),
                    Latitude  = area.Latitude + latJitter,
                    Longitude = area.Longitude + lonJitter
                });
            }
        }
        return pins;
    }

    public async Task<DubaiAveragesDto> GetDubaiAveragesAsync()
    {
        var data = await GetAreaMapDataAsync();
        return new DubaiAveragesDto
        {
            AvgPricePerSqft = Math.Round(data.Average(d => d.AvgPricePerSqft), 0),
            AvgTransactionCount = data.Average(d => d.TransactionCount),
            AvgTotalValue = data.Average(d => d.TotalValue)
        };
    }

    /// <summary>Render a rounded-bar sparkline as inline SVG markup.</summary>
    public static string RenderSparkline(double[] values, string color, int width = 100, int height = 32)
    {
        if (values == null || values.Length == 0) return "";
        var max = values.Max();
        if (max <= 0) max = 1;
        var step = (double)width / values.Length;
        var barW = Math.Max(2, step - 2);
        var sb = new System.Text.StringBuilder();
        sb.Append($"<svg viewBox='0 0 {width} {height}' xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}'>");
        for (int i = 0; i < values.Length; i++)
        {
            var h = Math.Max(2, values[i] / max * height);
            var x = i * step + 1;
            var y = height - h;
            sb.Append($"<rect x='{x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}' y='{y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}' width='{barW.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}' height='{h.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}' rx='1.5' fill='{color}' fill-opacity='0.78'/>");
        }
        sb.Append("</svg>");
        return sb.ToString();
    }

    public static string FormatAed(decimal amount)
    {
        if (amount >= 1_000_000_000) return $"AED {amount / 1_000_000_000:N2}B";
        if (amount >= 1_000_000) return $"AED {amount / 1_000_000:N2}M";
        if (amount >= 1_000) return $"AED {amount / 1_000:N1}K";
        return $"AED {amount:N0}";
    }
}
