using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using IRETP.Application.DTOs;
using IRETP.Application.Features.Analytics.Queries;
using MediatR;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IRETP.Application.Features.Export.Commands;

public class ExportAnalyticsCommandHandler : IRequestHandler<ExportAnalyticsCommand, ExportResult>
{
    private readonly IMediator _mediator;

    public ExportAnalyticsCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<ExportResult> Handle(ExportAnalyticsCommand request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ExecuteAnalyticsQuery { Request = request.Request }, cancellationToken);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        return request.Format.ToLowerInvariant() switch
        {
            "csv" => GenerateCsv(result, timestamp),
            "pdf" => GeneratePdf(result, request.Request, timestamp),
            "json" => GenerateJson(result, timestamp),
            _ => GenerateExcel(result, request.Request, timestamp)
        };
    }

    // -------------------------------------------------------------------------
    // Excel (.xlsx) with summary sheet
    // -------------------------------------------------------------------------
    private static ExportResult GenerateExcel(
        AnalyticsResultDto result, AnalyticsQueryRequest request, string timestamp)
    {
        using var workbook = new XLWorkbook();

        // Metadata
        var meta = workbook.Worksheets.Add("Metadata");
        meta.Cell(1, 1).Value = "Dubai Land Department — IRETP Analytics Export";
        meta.Cell(1, 1).Style.Font.Bold = true;
        meta.Cell(1, 1).Style.Font.FontSize = 14;
        meta.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
        meta.Cell(3, 1).Value = $"Data source: DLD transaction registry (RFP Section 12 validated)";
        meta.Cell(4, 1).Value = "Disclaimer: For market transparency purposes only. Not investment advice.";

        var metaRow = 6;
        meta.Cell(metaRow++, 1).Value = "Query configuration";
        meta.Cell(metaRow - 1, 1).Style.Font.Bold = true;
        meta.Cell(metaRow++, 1).Value = $"Dimensions: {string.Join(", ", result.Dimensions)}";
        meta.Cell(metaRow++, 1).Value = $"Metrics: {string.Join(", ", result.Metrics)}";
        if (request.DateFrom.HasValue) meta.Cell(metaRow++, 1).Value = $"Date from: {request.DateFrom:yyyy-MM-dd}";
        if (request.DateTo.HasValue) meta.Cell(metaRow++, 1).Value = $"Date to: {request.DateTo:yyyy-MM-dd}";
        if (request.ZoneIds is { Count: > 0 })
            meta.Cell(metaRow++, 1).Value = $"Zone IDs: {string.Join(", ", request.ZoneIds)}";
        if (request.PropertyTypes is { Count: > 0 })
            meta.Cell(metaRow++, 1).Value = $"Property types: {string.Join(", ", request.PropertyTypes)}";
        if (request.TransactionTypes is { Count: > 0 })
            meta.Cell(metaRow++, 1).Value = $"Transaction types: {string.Join(", ", request.TransactionTypes)}";
        meta.Cell(metaRow++, 1).Value = $"Recommended chart: {result.RecommendedChartType}";
        meta.Columns().AdjustToContents();

        // Summary statistics
        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = "Metric";
        summary.Cell(1, 2).Value = "Value";
        summary.Range(1, 1, 1, 2).Style.Font.Bold = true;
        summary.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4F72");
        summary.Range(1, 1, 1, 2).Style.Font.FontColor = XLColor.White;

        var summaryIdx = 2;
        foreach (var kv in result.SummaryStatistics)
        {
            summary.Cell(summaryIdx, 1).Value = kv.Key;
            summary.Cell(summaryIdx, 2).Value = (double)kv.Value;
            summaryIdx++;
        }
        summary.Columns().AdjustToContents();

        // Data
        var data = workbook.Worksheets.Add("Data");
        var columns = result.Dimensions.Concat(result.Metrics).ToList();
        for (var col = 0; col < columns.Count; col++)
        {
            var cell = data.Cell(1, col + 1);
            cell.Value = columns[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4F72");
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (var r = 0; r < result.Data.Count; r++)
        {
            var row = result.Data[r];
            for (var c = 0; c < columns.Count; c++)
            {
                var key = columns[c];
                var value = row.TryGetValue(key, out var v) ? v : "";
                WriteCellValue(data.Cell(r + 2, c + 1), value);
            }
        }
        data.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return new ExportResult
        {
            FileContent = ms.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = $"IRETP_Analytics_{timestamp}.xlsx"
        };
    }

    private static void WriteCellValue(IXLCell cell, object value)
    {
        switch (value)
        {
            case decimal d: cell.Value = (double)d; break;
            case double db: cell.Value = db; break;
            case int i: cell.Value = i; break;
            case long l: cell.Value = l; break;
            case bool b: cell.Value = b; break;
            case DateTime dt: cell.Value = dt; break;
            case null: cell.Value = ""; break;
            default: cell.Value = value.ToString() ?? ""; break;
        }
    }

    // -------------------------------------------------------------------------
    // CSV (raw data)
    // -------------------------------------------------------------------------
    private static ExportResult GenerateCsv(AnalyticsResultDto result, string timestamp)
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            var columns = result.Dimensions.Concat(result.Metrics).ToList();
            foreach (var col in columns) csv.WriteField(col);
            csv.NextRecord();

            foreach (var row in result.Data)
            {
                foreach (var col in columns)
                {
                    csv.WriteField(row.TryGetValue(col, out var v) ? v : "");
                }
                csv.NextRecord();
            }
            writer.Flush();
        }

        return new ExportResult
        {
            FileContent = ms.ToArray(),
            ContentType = "text/csv",
            FileName = $"IRETP_Analytics_{timestamp}.csv"
        };
    }

    // -------------------------------------------------------------------------
    // JSON (for API consumers)
    // -------------------------------------------------------------------------
    private static ExportResult GenerateJson(AnalyticsResultDto result, string timestamp)
    {
        var envelope = new
        {
            generatedAt = DateTime.UtcNow,
            source = "Dubai Land Department — IRETP Analytics API",
            disclaimer = "For market transparency purposes only. Not investment advice.",
            query = new { result.Dimensions, result.Metrics, result.RecommendedChartType },
            summary = result.SummaryStatistics,
            data = result.Data
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        return new ExportResult
        {
            FileContent = System.Text.Encoding.UTF8.GetBytes(json),
            ContentType = "application/json",
            FileName = $"IRETP_Analytics_{timestamp}.json"
        };
    }

    // -------------------------------------------------------------------------
    // PDF with DLD letterhead + embedded bar chart of first metric
    // -------------------------------------------------------------------------
    private static ExportResult GeneratePdf(
        AnalyticsResultDto result, AnalyticsQueryRequest request, string timestamp)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var primaryMetric = result.Metrics.FirstOrDefault() ?? "TransactionCount";
        var primaryDimension = result.Dimensions.FirstOrDefault() ?? "Zone";

        // Extract chart series (top 15 by primary metric, desc)
        var chartRows = result.Data
            .Select(d => new
            {
                Label = d.TryGetValue(primaryDimension, out var lbl) ? lbl?.ToString() ?? "—" : "—",
                Value = ExtractDecimal(d, primaryMetric)
            })
            .OrderByDescending(x => x.Value)
            .Take(15)
            .ToList();

        var maxValue = chartRows.Count > 0 ? chartRows.Max(r => r.Value) : 1m;
        if (maxValue == 0m) maxValue = 1m;

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
                    col.Item().Text("IRETP — Analytics Export Report").FontSize(12).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                page.Content().PaddingVertical(6).Column(col =>
                {
                    col.Item().PaddingBottom(6).Text("Query configuration").Bold().FontSize(11);
                    col.Item().Text($"Dimensions: {string.Join(", ", result.Dimensions)}");
                    col.Item().Text($"Metrics: {string.Join(", ", result.Metrics)}");
                    if (request.DateFrom.HasValue || request.DateTo.HasValue)
                    {
                        col.Item().Text($"Period: {request.DateFrom:yyyy-MM-dd} → {request.DateTo:yyyy-MM-dd}");
                    }
                    col.Item().Text($"Recommended chart: {result.RecommendedChartType}");

                    // Summary statistics box
                    col.Item().PaddingVertical(8).Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text("Summary statistics").Bold().FontSize(10);
                        foreach (var kv in result.SummaryStatistics)
                        {
                            c.Item().Text($"{kv.Key}: {FormatDecimal(kv.Value)}");
                        }
                    });

                    // Embedded bar chart (QuestPDF primitives only — no external image lib)
                    col.Item().PaddingTop(6).Text($"Top {chartRows.Count} by {primaryMetric}").Bold().FontSize(10);
                    col.Item().PaddingTop(4).Column(chart =>
                    {
                        foreach (var r in chartRows)
                        {
                            var pct = (float)Math.Max(0.02, (double)r.Value / (double)maxValue);
                            chart.Item().PaddingVertical(2).Row(row =>
                            {
                                row.ConstantItem(130).Text(Truncate(r.Label, 28)).FontSize(8);
                                row.RelativeItem().Element(container =>
                                {
                                    container.Height(10).Background(Colors.Grey.Lighten3)
                                        .AlignLeft().Width(pct * 280).Background(Colors.Blue.Darken2);
                                });
                                row.ConstantItem(80).AlignRight().Text(FormatDecimal(r.Value)).FontSize(8);
                            });
                        }
                    });

                    // Data table
                    col.Item().PaddingTop(10).Text("Full result").Bold().FontSize(10);
                    col.Item().PaddingTop(4).Table(table =>
                    {
                        var columnNames = result.Dimensions.Concat(result.Metrics).ToList();
                        table.ColumnsDefinition(columns =>
                        {
                            for (var i = 0; i < columnNames.Count; i++) columns.RelativeColumn();
                        });

                        foreach (var c in columnNames)
                        {
                            table.Cell().Background(Colors.Blue.Darken3).Padding(3)
                                .Text(c).FontColor(Colors.White).Bold().FontSize(8);
                        }

                        for (var r = 0; r < result.Data.Count; r++)
                        {
                            var bg = r % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                            foreach (var c in columnNames)
                            {
                                var raw = result.Data[r].TryGetValue(c, out var v) ? v : "";
                                table.Cell().Background(bg).Padding(2)
                                    .Text(FormatCell(raw)).FontSize(7);
                            }
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                    text.Span(" · Dubai Land Department — Confidential");
                });
            });
        });

        return new ExportResult
        {
            FileContent = document.GeneratePdf(),
            ContentType = "application/pdf",
            FileName = $"IRETP_Analytics_{timestamp}.pdf"
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private static decimal ExtractDecimal(IDictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null) return 0m;
        return value switch
        {
            decimal d => d,
            double db => (decimal)db,
            int i => i,
            long l => l,
            string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0m
        };
    }

    private static string FormatDecimal(decimal value) =>
        value % 1 == 0 ? value.ToString("N0") : value.ToString("N2");

    private static string FormatCell(object value) => value switch
    {
        null => "",
        decimal d => FormatDecimal(d),
        double db => FormatDecimal((decimal)db),
        DateTime dt => dt.ToString("yyyy-MM-dd"),
        _ => value.ToString() ?? ""
    };

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) ? "" : value.Length <= max ? value : value[..(max - 1)] + "…";
}
