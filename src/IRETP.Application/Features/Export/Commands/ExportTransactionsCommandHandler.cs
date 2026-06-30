using System.Globalization;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IRETP.Application.Features.Export.Commands;

public class ExportTransactionsCommandHandler
    : IRequestHandler<ExportTransactionsCommand, ExportResult>
{
    private const int MaxRows = 50_000;
    private readonly IRepository<Transaction> _transactionRepo;

    public ExportTransactionsCommandHandler(IRepository<Transaction> transactionRepo)
    {
        _transactionRepo = transactionRepo;
    }

    public async Task<ExportResult> Handle(
        ExportTransactionsCommand request, CancellationToken cancellationToken)
    {
        var transactions = GetFilteredTransactions(request);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

        return request.Format.ToLowerInvariant() switch
        {
            "csv" => await Task.FromResult(GenerateCsv(transactions, timestamp)),
            "pdf" => await Task.FromResult(GeneratePdf(transactions, request, timestamp)),
            _ => await Task.FromResult(GenerateExcel(transactions, request, timestamp))
        };
    }

    private List<TransactionExportRow> GetFilteredTransactions(ExportTransactionsCommand request)
    {
        var query = _transactionRepo.Query().AsQueryable();

        if (request.DateFrom.HasValue)
            query = query.Where(t => t.TransactionDate >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(t => t.TransactionDate <= request.DateTo.Value);

        if (request.ZoneIds is { Count: > 0 })
            query = query.Where(t => request.ZoneIds.Contains(t.ZoneId));

        if (request.PropertyTypes is { Count: > 0 })
            query = query.Where(t => request.PropertyTypes.Contains(t.PropertyType));

        if (request.TransactionTypes is { Count: > 0 })
            query = query.Where(t => request.TransactionTypes.Contains(t.TransactionType));

        if (request.PriceMin.HasValue)
            query = query.Where(t => t.TransactionValue >= request.PriceMin.Value);

        if (request.PriceMax.HasValue)
            query = query.Where(t => t.TransactionValue <= request.PriceMax.Value);

        if (request.AreaMin.HasValue)
            query = query.Where(t => t.AreaSqft >= request.AreaMin.Value);

        if (request.AreaMax.HasValue)
            query = query.Where(t => t.AreaSqft <= request.AreaMax.Value);

        if (request.FinancingMethod.HasValue)
            query = query.Where(t => t.FinancingMethod == request.FinancingMethod.Value);

        return query
            .OrderByDescending(t => t.TransactionDate)
            .Take(MaxRows)
            .Select(t => new TransactionExportRow
            {
                TransactionDate = t.TransactionDate,
                Zone = t.Zone.Name,
                Community = t.Community ?? "",
                Project = t.ProjectName ?? "",
                PropertyType = t.PropertyType.ToString(),
                TransactionType = t.TransactionType.ToString(),
                AreaSqft = t.AreaSqft,
                AreaSqm = t.AreaSqm,
                ValueAed = t.TransactionValue,
                PricePerSqft = t.PricePerSqft,
                FinancingMethod = t.FinancingMethod.ToString()
            })
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Excel (ClosedXML)
    // -------------------------------------------------------------------------
    private static ExportResult GenerateExcel(
        List<TransactionExportRow> rows, ExportTransactionsCommand request, string timestamp)
    {
        using var workbook = new XLWorkbook();

        // -- Metadata sheet --
        var meta = workbook.Worksheets.Add("Metadata");
        meta.Cell(1, 1).Value = "Dubai Land Department - IRETP Transaction Export";
        meta.Cell(1, 1).Style.Font.Bold = true;
        meta.Cell(1, 1).Style.Font.FontSize = 14;
        meta.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC";
        meta.Cell(3, 1).Value = $"Total Records: {rows.Count:N0}";

        var metaRow = 5;
        meta.Cell(metaRow, 1).Value = "Applied Filters";
        meta.Cell(metaRow, 1).Style.Font.Bold = true;
        metaRow++;

        if (request.DateFrom.HasValue)
            meta.Cell(metaRow++, 1).Value = $"Date From: {request.DateFrom.Value:yyyy-MM-dd}";
        if (request.DateTo.HasValue)
            meta.Cell(metaRow++, 1).Value = $"Date To: {request.DateTo.Value:yyyy-MM-dd}";
        if (request.ZoneIds is { Count: > 0 })
            meta.Cell(metaRow++, 1).Value = $"Zone IDs: {string.Join(", ", request.ZoneIds)}";
        if (request.PropertyTypes is { Count: > 0 })
            meta.Cell(metaRow++, 1).Value = $"Property Types: {string.Join(", ", request.PropertyTypes)}";
        if (request.TransactionTypes is { Count: > 0 })
            meta.Cell(metaRow++, 1).Value = $"Transaction Types: {string.Join(", ", request.TransactionTypes)}";
        if (request.PriceMin.HasValue)
            meta.Cell(metaRow++, 1).Value = $"Price Min: {request.PriceMin.Value:N2} AED";
        if (request.PriceMax.HasValue)
            meta.Cell(metaRow++, 1).Value = $"Price Max: {request.PriceMax.Value:N2} AED";
        if (request.AreaMin.HasValue)
            meta.Cell(metaRow++, 1).Value = $"Area Min: {request.AreaMin.Value:N2} sqft";
        if (request.AreaMax.HasValue)
            meta.Cell(metaRow++, 1).Value = $"Area Max: {request.AreaMax.Value:N2} sqft";
        if (request.FinancingMethod.HasValue)
            meta.Cell(metaRow++, 1).Value = $"Financing Method: {request.FinancingMethod.Value}";

        meta.Columns().AdjustToContents();

        // -- Data sheet --
        var data = workbook.Worksheets.Add("Transactions");
        var headers = new[]
        {
            "Transaction Date", "Zone", "Community", "Project",
            "Property Type", "Transaction Type", "Area (sqft)", "Area (sqm)",
            "Value (AED)", "Price/Sqft", "Financing Method"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = data.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4F72");
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var row = i + 2;
            data.Cell(row, 1).Value = r.TransactionDate.ToString("yyyy-MM-dd");
            data.Cell(row, 2).Value = r.Zone;
            data.Cell(row, 3).Value = r.Community;
            data.Cell(row, 4).Value = r.Project;
            data.Cell(row, 5).Value = r.PropertyType;
            data.Cell(row, 6).Value = r.TransactionType;
            data.Cell(row, 7).Value = r.AreaSqft;
            data.Cell(row, 8).Value = r.AreaSqm;
            data.Cell(row, 9).Value = r.ValueAed;
            data.Cell(row, 10).Value = r.PricePerSqft;
            data.Cell(row, 11).Value = r.FinancingMethod;
        }

        // Summary row
        if (rows.Count > 0)
        {
            var summaryRow = rows.Count + 3;
            data.Cell(summaryRow, 1).Value = "SUMMARY";
            data.Cell(summaryRow, 1).Style.Font.Bold = true;
            data.Cell(summaryRow, 6).Value = "Total:";
            data.Cell(summaryRow, 6).Style.Font.Bold = true;
            data.Cell(summaryRow, 7).Value = rows.Sum(r => r.AreaSqft);
            data.Cell(summaryRow, 9).Value = rows.Sum(r => r.ValueAed);
            data.Cell(summaryRow, 10).Value = rows.Average(r => r.PricePerSqft);

            data.Cell(summaryRow + 1, 6).Value = "Count:";
            data.Cell(summaryRow + 1, 6).Style.Font.Bold = true;
            data.Cell(summaryRow + 1, 7).Value = rows.Count;
        }

        data.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        return new ExportResult
        {
            FileContent = ms.ToArray(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName = $"IRETP_Transactions_{timestamp}.xlsx"
        };
    }

    // -------------------------------------------------------------------------
    // CSV (CsvHelper)
    // -------------------------------------------------------------------------
    private static ExportResult GenerateCsv(List<TransactionExportRow> rows, string timestamp)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));

        csv.WriteRecords(rows);
        writer.Flush();

        return new ExportResult
        {
            FileContent = ms.ToArray(),
            ContentType = "text/csv",
            FileName = $"IRETP_Transactions_{timestamp}.csv"
        };
    }

    // -------------------------------------------------------------------------
    // PDF (QuestPDF)
    // -------------------------------------------------------------------------
    private static ExportResult GeneratePdf(
        List<TransactionExportRow> rows, ExportTransactionsCommand request, string timestamp)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(8));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Dubai Land Department")
                            .Bold().FontSize(16).FontColor(Colors.Blue.Darken3);
                    });
                    col.Item().Text("IRETP - Transaction Export Report")
                        .FontSize(12).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Records: {rows.Count:N0}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                // Content
                page.Content().PaddingVertical(5).Column(col =>
                {
                    // Summary statistics
                    if (rows.Count > 0)
                    {
                        col.Item().PaddingBottom(10).Row(row =>
                        {
                            row.RelativeItem().Background(Colors.Grey.Lighten4).Padding(8).Column(c =>
                            {
                                c.Item().Text("Summary Statistics").Bold().FontSize(10);
                                c.Item().Text($"Total Transactions: {rows.Count:N0}");
                                c.Item().Text($"Total Value: {rows.Sum(r => r.ValueAed):N2} AED");
                                c.Item().Text($"Average Price/Sqft: {rows.Average(r => r.PricePerSqft):N2} AED");
                                c.Item().Text($"Total Area: {rows.Sum(r => r.AreaSqft):N2} sqft");
                            });
                        });
                    }

                    // Data table
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.2f); // Date
                            columns.RelativeColumn(1.5f); // Zone
                            columns.RelativeColumn(1.2f); // Community
                            columns.RelativeColumn(1.2f); // Project
                            columns.RelativeColumn(1f);   // Property Type
                            columns.RelativeColumn(0.8f); // Transaction Type
                            columns.RelativeColumn(1f);   // Area sqft
                            columns.RelativeColumn(1f);   // Value
                            columns.RelativeColumn(0.9f); // Price/sqft
                            columns.RelativeColumn(0.9f); // Financing
                        });

                        // Header row
                        var headerLabels = new[]
                        {
                            "Date", "Zone", "Community", "Project",
                            "Property", "Txn Type", "Area (sqft)",
                            "Value (AED)", "Price/Sqft", "Financing"
                        };

                        foreach (var header in headerLabels)
                        {
                            table.Cell().Background(Colors.Blue.Darken3)
                                .Padding(3)
                                .Text(header).FontColor(Colors.White).Bold().FontSize(7);
                        }

                        // Data rows
                        for (var i = 0; i < rows.Count; i++)
                        {
                            var r = rows[i];
                            var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                            table.Cell().Background(bg).Padding(2).Text(r.TransactionDate.ToString("yyyy-MM-dd")).FontSize(7);
                            table.Cell().Background(bg).Padding(2).Text(r.Zone).FontSize(7);
                            table.Cell().Background(bg).Padding(2).Text(r.Community).FontSize(7);
                            table.Cell().Background(bg).Padding(2).Text(r.Project).FontSize(7);
                            table.Cell().Background(bg).Padding(2).Text(r.PropertyType).FontSize(7);
                            table.Cell().Background(bg).Padding(2).Text(r.TransactionType).FontSize(7);
                            table.Cell().Background(bg).Padding(2).AlignRight().Text($"{r.AreaSqft:N2}").FontSize(7);
                            table.Cell().Background(bg).Padding(2).AlignRight().Text($"{r.ValueAed:N2}").FontSize(7);
                            table.Cell().Background(bg).Padding(2).AlignRight().Text($"{r.PricePerSqft:N2}").FontSize(7);
                            table.Cell().Background(bg).Padding(2).Text(r.FinancingMethod).FontSize(7);
                        }
                    });
                });

                // Footer
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                    text.Span(" | Dubai Land Department - Confidential");
                });
            });
        });

        var pdfBytes = document.GeneratePdf();

        return new ExportResult
        {
            FileContent = pdfBytes,
            ContentType = "application/pdf",
            FileName = $"IRETP_Transactions_{timestamp}.pdf"
        };
    }
}

internal class TransactionExportRow
{
    public DateTime TransactionDate { get; set; }
    public string Zone { get; set; } = "";
    public string Community { get; set; } = "";
    public string Project { get; set; } = "";
    public string PropertyType { get; set; } = "";
    public string TransactionType { get; set; } = "";
    public decimal AreaSqft { get; set; }
    public decimal AreaSqm { get; set; }
    public decimal ValueAed { get; set; }
    public decimal PricePerSqft { get; set; }
    public string FinancingMethod { get; set; } = "";
}
