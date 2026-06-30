using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IRETP.Web.Services;

/// <summary>
/// Renders transaction lists into Excel / CSV / PDF payloads for client
/// download, satisfying RFP FR008. Runs in-process to keep the public portal
/// independent of the backend when fixture data is in play; the same packages
/// are used by the backend's ExportTransactionsCommandHandler so behaviour
/// matches production.
/// </summary>
public class TransactionExportService
{
    public sealed record ExportPayload(byte[] Content, string ContentType, string FileName);

    public enum Format { Excel, Csv, Pdf }

    public ExportPayload Export(IEnumerable<SalesTxnDto> rows, Format format,
        string? filterSummary = null)
    {
        var list = rows.ToList();
        var ts = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return format switch
        {
            Format.Csv  => ToCsv(list, ts),
            Format.Pdf  => ToPdf(list, ts, filterSummary),
            _           => ToExcel(list, ts, filterSummary)
        };
    }

    // -------------------------------------------------------------------------
    // Excel
    // -------------------------------------------------------------------------
    private static ExportPayload ToExcel(List<SalesTxnDto> rows, string ts, string? filterSummary)
    {
        using var workbook = new XLWorkbook();

        var meta = workbook.Worksheets.Add("Metadata");
        meta.Cell(1, 1).Value = "Dubai Land Department — IRETP Transaction Export";
        meta.Cell(1, 1).Style.Font.Bold = true;
        meta.Cell(1, 1).Style.Font.FontSize = 14;
        meta.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
        meta.Cell(3, 1).Value = $"Total Records: {rows.Count:N0}";
        if (!string.IsNullOrWhiteSpace(filterSummary))
        {
            meta.Cell(5, 1).Value = "Applied Filters";
            meta.Cell(5, 1).Style.Font.Bold = true;
            meta.Cell(6, 1).Value = filterSummary;
        }
        meta.Columns().AdjustToContents();

        var data = workbook.Worksheets.Add("Transactions");
        string[] headers =
        {
            "Transaction Date", "Area", "Building", "Property Type", "Transaction Type",
            "Rooms", "Size (sqft)", "Value (AED)", "Price/Sqft"
        };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = data.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#066735");
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var row = i + 2;
            data.Cell(row, 1).Value = r.TransactionDate.ToString("yyyy-MM-dd");
            data.Cell(row, 2).Value = r.AreaName;
            data.Cell(row, 3).Value = r.BuildingName;
            data.Cell(row, 4).Value = r.PropertyType;
            data.Cell(row, 5).Value = r.TransactionType;
            data.Cell(row, 6).Value = r.RoomCount;
            data.Cell(row, 7).Value = r.PropertySizeSqft;
            data.Cell(row, 8).Value = r.AmountAed;
            data.Cell(row, 9).Value = r.PricePerSqft;
        }

        if (rows.Count > 0)
        {
            var summary = rows.Count + 3;
            data.Cell(summary, 1).Value = "SUMMARY";
            data.Cell(summary, 1).Style.Font.Bold = true;
            data.Cell(summary, 6).Value = "Total:";
            data.Cell(summary, 6).Style.Font.Bold = true;
            data.Cell(summary, 7).Value = rows.Sum(r => r.PropertySizeSqft);
            data.Cell(summary, 8).Value = rows.Sum(r => r.AmountAed);
            data.Cell(summary, 9).Value = rows.Average(r => r.PricePerSqft);
        }
        data.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return new ExportPayload(ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"IRETP_Transactions_{ts}.xlsx");
    }

    // -------------------------------------------------------------------------
    // CSV
    // -------------------------------------------------------------------------
    private static ExportPayload ToCsv(List<SalesTxnDto> rows, string ts)
    {
        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            csv.WriteRecords(rows.Select(r => new
            {
                TransactionDate = r.TransactionDate.ToString("yyyy-MM-dd"),
                r.PropertyType,
                r.AreaName,
                r.BuildingName,
                r.RoomCount,
                r.PropertySizeSqft,
                AmountAed = (decimal)r.AmountAed,
                PricePerSqft = Math.Round(r.PricePerSqft, 2),
                r.TransactionType
            }));
            writer.Flush();
        }
        return new ExportPayload(ms.ToArray(), "text/csv", $"IRETP_Transactions_{ts}.csv");
    }

    // -------------------------------------------------------------------------
    // PDF (QuestPDF)
    // -------------------------------------------------------------------------
    private static ExportPayload ToPdf(List<SalesTxnDto> rows, string ts, string? filterSummary)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontSize(8));

            page.Header().Column(col =>
            {
                col.Item().Text("Dubai Land Department").Bold().FontSize(16).FontColor(Colors.Green.Darken3);
                col.Item().Text("IRETP — Transaction Export Report").FontSize(12).FontColor(Colors.Grey.Darken2);
                col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Records: {rows.Count:N0}")
                    .FontSize(9).FontColor(Colors.Grey.Medium);
                if (!string.IsNullOrWhiteSpace(filterSummary))
                    col.Item().Text($"Filters: {filterSummary}").FontSize(9).FontColor(Colors.Grey.Medium);
                col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Green.Darken3);
            });

            page.Content().PaddingVertical(5).Column(col =>
            {
                if (rows.Count > 0)
                {
                    col.Item().PaddingBottom(10).Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                    {
                        c.Item().Text("Summary Statistics").Bold().FontSize(10);
                        c.Item().Text($"Total Transactions: {rows.Count:N0}");
                        c.Item().Text($"Total Value: {rows.Sum(r => r.AmountAed):N2} AED");
                        c.Item().Text($"Average Price/Sqft: {rows.Average(r => r.PricePerSqft):N2} AED");
                        c.Item().Text($"Total Area: {rows.Sum(r => r.PropertySizeSqft):N0} sqft");
                    });
                }

                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.4f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.0f);
                        columns.RelativeColumn(1.0f);
                        columns.RelativeColumn(0.7f);
                        columns.RelativeColumn(1.0f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(0.9f);
                    });
                    string[] headers =
                    {
                        "Date", "Area", "Building", "Property", "Status",
                        "Rooms", "Size (sqft)", "Value (AED)", "Price/Sqft"
                    };
                    foreach (var h in headers)
                        t.Cell().Background(Colors.Green.Darken3).Padding(3)
                            .Text(h).FontColor(Colors.White).Bold().FontSize(7);

                    for (var i = 0; i < rows.Count; i++)
                    {
                        var r = rows[i];
                        var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                        t.Cell().Background(bg).Padding(2).Text(r.TransactionDate.ToString("yyyy-MM-dd")).FontSize(7);
                        t.Cell().Background(bg).Padding(2).Text(r.AreaName).FontSize(7);
                        t.Cell().Background(bg).Padding(2).Text(r.BuildingName).FontSize(7);
                        t.Cell().Background(bg).Padding(2).Text(r.PropertyType).FontSize(7);
                        t.Cell().Background(bg).Padding(2).Text(r.TransactionType).FontSize(7);
                        t.Cell().Background(bg).Padding(2).AlignRight().Text(r.RoomCount.ToString()).FontSize(7);
                        t.Cell().Background(bg).Padding(2).AlignRight().Text($"{r.PropertySizeSqft:N0}").FontSize(7);
                        t.Cell().Background(bg).Padding(2).AlignRight().Text($"{r.AmountAed:N0}").FontSize(7);
                        t.Cell().Background(bg).Padding(2).AlignRight().Text($"{r.PricePerSqft:N0}").FontSize(7);
                    }
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
                text.Span(" | Dubai Land Department — Confidential");
            });
        }));

        return new ExportPayload(doc.GeneratePdf(), "application/pdf",
            $"IRETP_Transactions_{ts}.pdf");
    }
}
