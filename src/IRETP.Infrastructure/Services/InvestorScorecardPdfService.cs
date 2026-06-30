using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Builds the PDF Investment Profile (RFP Section 20 — Phase 4 deliverable).
/// Produces two report shapes from the same engine: zone-level and
/// project-level. Both carry DLD branding, a summary KPI block, an embedded
/// bar chart of the main metric, and source disclaimers. The 10-second
/// generation target (RFP phase 4 deliverable #52) is met by staying inside
/// QuestPDF and avoiding image/remote fetches.
/// </summary>
public class InvestorScorecardPdfService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public InvestorScorecardPdfService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<byte[]> RenderZoneAsync(Guid zoneId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var zoneRepo = scope.ServiceProvider.GetRequiredService<IRepository<Zone>>();
        var txnRepo = scope.ServiceProvider.GetRequiredService<IRepository<Transaction>>();
        var priceRepo = scope.ServiceProvider.GetRequiredService<IRepository<PriceIndex>>();
        var rentalRepo = scope.ServiceProvider.GetRequiredService<IRepository<RentalIndex>>();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IRepository<Project>>();

        var zone = await zoneRepo.GetByIdAsync(zoneId, ct)
                   ?? throw new InvalidOperationException($"Zone {zoneId} not found.");

        var cutoff = DateTime.UtcNow.AddMonths(-12);
        var transactions = await txnRepo.Query()
            .Where(t => t.ZoneId == zoneId && t.TransactionDate >= cutoff)
            .ToListAsync(ct);

        var latestPrice = await priceRepo.Query()
            .Where(p => p.ZoneId == zoneId)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).ThenByDescending(p => p.Month)
            .FirstOrDefaultAsync(ct);

        var latestRental = await rentalRepo.Query()
            .Where(r => r.ZoneId == zoneId)
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Quarter)
            .FirstOrDefaultAsync(ct);

        var projects = await projectRepo.Query()
            .Where(p => p.ZoneId == zoneId)
            .Include(p => p.Developer)
            .ToListAsync(ct);

        var model = new ZoneReportModel
        {
            ZoneName = zone.Name,
            ZoneNameAr = zone.NameAr,
            TransactionsLast12m = transactions.Count,
            TotalValueLast12m = transactions.Sum(t => t.TransactionValue),
            AvgPricePerSqft = latestPrice?.AveragePricePerSqft ?? 0m,
            QoQPriceChange = latestPrice?.QuarterlyChange ?? 0m,
            YoYPriceChange = latestPrice?.AnnualChange ?? 0m,
            AvgRentalYield = latestRental?.GrossRentalYield ?? 0m,
            ActiveProjects = projects.Count(p => p.Status != ProjectStatus.Completed),
            CompletedProjects = projects.Count(p => p.Status == ProjectStatus.Completed),
            Top5DevelopersByValue = transactions
                .Where(t => t.Project != null)
                .GroupBy(t => t.Project?.Developer?.Name ?? "—")
                .Select(g => (Label: g.Key, Value: g.Sum(x => x.TransactionValue)))
                .OrderByDescending(x => x.Value)
                .Take(5)
                .ToList()
        };

        return Render(model);
    }

    public async Task<byte[]> RenderProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IRepository<Project>>();
        var txnRepo = scope.ServiceProvider.GetRequiredService<IRepository<Transaction>>();
        var escrowRepo = scope.ServiceProvider.GetRequiredService<IRepository<EscrowAccount>>();

        var project = await projectRepo.Query()
            .Include(p => p.Developer)
            .Include(p => p.Zone)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var txns = await txnRepo.Query()
            .Where(t => t.ProjectId == projectId)
            .OrderByDescending(t => t.TransactionDate)
            .Take(200)
            .ToListAsync(ct);

        var escrow = await escrowRepo.Query()
            .FirstOrDefaultAsync(e => e.ProjectId == projectId, ct);

        var model = new ProjectReportModel
        {
            ProjectName = project.Name,
            ProjectNameAr = project.NameAr,
            DeveloperName = project.Developer?.Name ?? "—",
            ZoneName = project.Zone?.Name ?? "—",
            Status = project.Status.ToString(),
            CompletionPercentage = project.CompletionPercentage,
            TotalUnits = project.TotalUnits,
            SoldUnits = project.SoldUnits,
            AvailableUnits = project.AvailableUnits,
            ExpectedDelivery = project.ExpectedDeliveryDate,
            Transactions = txns.Count,
            TotalValue = txns.Sum(t => t.TransactionValue),
            AvgPricePerSqft = txns.Count == 0 ? 0m : txns.Average(t => t.PricePerSqft),
            EscrowBalance = escrow?.CurrentBalance,
            EscrowAdequacyRatio = escrow?.AdequacyRatio,
            MonthlyVolume = txns
                .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                .Select(g => (Label: $"{g.Key.Year:0000}-{g.Key.Month:00}", Value: (decimal)g.Count()))
                .OrderBy(x => x.Label)
                .TakeLast(12)
                .ToList()
        };

        return Render(model);
    }

    // -----------------------------------------------------------------------
    // Internal report models
    // -----------------------------------------------------------------------
    private sealed class ZoneReportModel
    {
        public string ZoneName { get; set; } = "";
        public string ZoneNameAr { get; set; } = "";
        public int TransactionsLast12m { get; set; }
        public decimal TotalValueLast12m { get; set; }
        public decimal AvgPricePerSqft { get; set; }
        public decimal QoQPriceChange { get; set; }
        public decimal YoYPriceChange { get; set; }
        public decimal AvgRentalYield { get; set; }
        public int ActiveProjects { get; set; }
        public int CompletedProjects { get; set; }
        public List<(string Label, decimal Value)> Top5DevelopersByValue { get; set; } = new();
    }

    private sealed class ProjectReportModel
    {
        public string ProjectName { get; set; } = "";
        public string ProjectNameAr { get; set; } = "";
        public string DeveloperName { get; set; } = "";
        public string ZoneName { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal CompletionPercentage { get; set; }
        public int TotalUnits { get; set; }
        public int SoldUnits { get; set; }
        public int AvailableUnits { get; set; }
        public DateTime? ExpectedDelivery { get; set; }
        public int Transactions { get; set; }
        public decimal TotalValue { get; set; }
        public decimal AvgPricePerSqft { get; set; }
        public decimal? EscrowBalance { get; set; }
        public decimal? EscrowAdequacyRatio { get; set; }
        public List<(string Label, decimal Value)> MonthlyVolume { get; set; } = new();
    }

    // -----------------------------------------------------------------------
    // Rendering
    // -----------------------------------------------------------------------
    private static byte[] Render(ZoneReportModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("Dubai Land Department")
                        .Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                    col.Item().Text($"IRETP — Investment Profile · {model.ZoneName}")
                        .FontSize(12).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · 12-month rolling window")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                page.Content().PaddingVertical(6).Column(col =>
                {
                    // Headline KPIs
                    col.Item().Row(row =>
                    {
                        KpiCell(row.RelativeItem(), "Transactions", model.TransactionsLast12m.ToString("N0"));
                        KpiCell(row.RelativeItem(), "Total value", $"AED {FormatCompact(model.TotalValueLast12m)}");
                        KpiCell(row.RelativeItem(), "Avg / sqft", $"AED {model.AvgPricePerSqft:N0}");
                        KpiCell(row.RelativeItem(), "Rental yield", $"{model.AvgRentalYield:F2}%");
                    });

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        KpiCell(row.RelativeItem(), "Active projects", model.ActiveProjects.ToString());
                        KpiCell(row.RelativeItem(), "Completed", model.CompletedProjects.ToString());
                        KpiCell(row.RelativeItem(), "QoQ price Δ", FormatPct(model.QoQPriceChange));
                        KpiCell(row.RelativeItem(), "YoY price Δ", FormatPct(model.YoYPriceChange));
                    });

                    // Top developers chart
                    if (model.Top5DevelopersByValue.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Top developers by transaction value (last 12 months)")
                            .Bold().FontSize(10);
                        col.Item().PaddingTop(4).Column(chart =>
                        {
                            var max = model.Top5DevelopersByValue.Max(x => x.Value);
                            foreach (var r in model.Top5DevelopersByValue)
                            {
                                var pct = max == 0 ? 0.02f : (float)(double)(r.Value / max);
                                chart.Item().PaddingVertical(3).Row(row =>
                                {
                                    row.ConstantItem(160).Text(Truncate(r.Label, 28)).FontSize(8);
                                    row.RelativeItem().Height(10).Background(Colors.Grey.Lighten3)
                                        .AlignLeft().Width(Math.Max(pct, 0.02f) * 300).Background(Colors.Blue.Darken2);
                                    row.ConstantItem(100).AlignRight().Text($"AED {FormatCompact(r.Value)}").FontSize(8);
                                });
                            }
                        });
                    }

                    col.Item().PaddingTop(14).Background(Colors.Grey.Lighten4).Padding(8)
                        .Text("Source: DLD transaction registry, Ejari rental data, RERA project registry. " +
                              "For market transparency purposes only. Not investment advice.")
                        .Italic().FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                    text.Span($" · Dubai Land Department · {model.ZoneName} investment profile");
                });
            });
        });

        return document.GeneratePdf();
    }

    private static byte[] Render(ProjectReportModel model)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text("Dubai Land Department")
                        .Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                    col.Item().Text($"IRETP — Investment Profile · {model.ProjectName}")
                        .FontSize(12).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Developer: {model.DeveloperName} · Zone: {model.ZoneName}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                page.Content().PaddingVertical(6).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        KpiCell(row.RelativeItem(), "Status", model.Status);
                        KpiCell(row.RelativeItem(), "Completion", $"{model.CompletionPercentage:F1}%");
                        KpiCell(row.RelativeItem(), "Total units", model.TotalUnits.ToString("N0"));
                        KpiCell(row.RelativeItem(), "Sold / available", $"{model.SoldUnits:N0} / {model.AvailableUnits:N0}");
                    });

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        KpiCell(row.RelativeItem(), "Transactions", model.Transactions.ToString("N0"));
                        KpiCell(row.RelativeItem(), "Total value", $"AED {FormatCompact(model.TotalValue)}");
                        KpiCell(row.RelativeItem(), "Avg / sqft", $"AED {model.AvgPricePerSqft:N0}");
                        KpiCell(row.RelativeItem(), "Expected delivery",
                            model.ExpectedDelivery?.ToString("yyyy-MM-dd") ?? "—");
                    });

                    // Escrow block
                    if (model.EscrowBalance.HasValue)
                    {
                        var ratio = (model.EscrowAdequacyRatio ?? 0m) * 100m;
                        var color = ratio >= 100 ? Colors.Green.Darken2
                            : ratio >= 80 ? Colors.Orange.Darken2
                            : Colors.Red.Darken2;

                        col.Item().PaddingTop(10).Background(color).Padding(8)
                            .Text($"Escrow balance: AED {FormatCompact(model.EscrowBalance.Value)} · " +
                                  $"adequacy ratio {ratio:F1}%")
                            .FontColor(Colors.White).Bold().FontSize(11);
                    }

                    // Monthly volume chart
                    if (model.MonthlyVolume.Count > 0)
                    {
                        col.Item().PaddingTop(12).Text("Monthly transaction volume (last 12 months)")
                            .Bold().FontSize(10);
                        col.Item().PaddingTop(4).Column(chart =>
                        {
                            var max = model.MonthlyVolume.Max(x => x.Value);
                            foreach (var r in model.MonthlyVolume)
                            {
                                var pct = max == 0 ? 0.02f : (float)(double)(r.Value / max);
                                chart.Item().PaddingVertical(3).Row(row =>
                                {
                                    row.ConstantItem(80).Text(r.Label).FontSize(8);
                                    row.RelativeItem().Height(10).Background(Colors.Grey.Lighten3)
                                        .AlignLeft().Width(Math.Max(pct, 0.02f) * 320).Background(Colors.Blue.Darken2);
                                    row.ConstantItem(60).AlignRight().Text($"{r.Value:N0}").FontSize(8);
                                });
                            }
                        });
                    }

                    col.Item().PaddingTop(14).Background(Colors.Grey.Lighten4).Padding(8)
                        .Text("Source: DLD transaction registry, RERA project registry, Escrow bank data feeds. " +
                              "For market transparency purposes only. Not investment advice.")
                        .Italic().FontSize(8).FontColor(Colors.Grey.Medium);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                    text.Span($" · Dubai Land Department · {model.ProjectName}");
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void KpiCell(QuestPDF.Infrastructure.IContainer container, string label, string value)
    {
        container.PaddingRight(4).Background(Colors.Grey.Lighten4).Padding(8).Column(col =>
        {
            col.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken2);
            col.Item().PaddingTop(2).Text(value).Bold().FontSize(13).FontColor(Colors.Blue.Darken3);
        });
    }

    private static string FormatCompact(decimal value)
    {
        var abs = Math.Abs(value);
        if (abs >= 1_000_000_000m) return $"{value / 1_000_000_000m:F2}B";
        if (abs >= 1_000_000m) return $"{value / 1_000_000m:F2}M";
        if (abs >= 1_000m) return $"{value / 1_000m:F1}K";
        return value.ToString("N0");
    }

    private static string FormatPct(decimal value) =>
        (value >= 0 ? "+" : "") + value.ToString("F2") + "%";

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..(max - 1)] + "…";
}
