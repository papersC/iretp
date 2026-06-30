using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IRETP.Web.Services;

/// <summary>
/// RFP ESC-003 — auto-generates a Monthly Escrow Health Report PDF per
/// project. Combines the balance snapshot, adequacy trajectory, red-flag
/// summary, and the immutable audit log (Section 8.4). Outputs DLD-branded
/// PDF bytes suitable for email attachment or direct download.
/// </summary>
public class EscrowReportService
{
    public sealed record Report(byte[] Pdf, string FileName);

    public Report Generate(EscrowItem project, IReadOnlyList<AdminFixtureService.EscrowAuditEntry> audit)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var deposits   = audit.Where(e => e.Amount > 0).Sum(e => e.Amount);
        var withdrawals = audit.Where(e => e.Amount < 0).Sum(e => e.Amount);
        var statusLabel = project.Status switch { 0 => "ADEQUATE", 1 => "WARNING", 2 => "CRITICAL", _ => "UNKNOWN" };
        var statusColor = project.Status switch { 0 => Colors.Green.Darken2, 1 => Colors.Orange.Darken2, _ => Colors.Red.Darken2 };

        var doc = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(col =>
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Dubai Land Department").Bold().FontSize(16).FontColor(Colors.Green.Darken3);
                        c.Item().Text("IRETP — Monthly Escrow Health Report").FontSize(11).FontColor(Colors.Grey.Darken2);
                        c.Item().Text($"Reporting period: month ending {DateTime.UtcNow:MMMM yyyy}").FontSize(9).FontColor(Colors.Grey.Medium);
                    });
                    r.ConstantItem(130).AlignRight().Text("CONFIDENTIAL")
                        .FontColor(Colors.Red.Darken2).Bold().FontSize(10);
                });
                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Green.Darken3);
            });

            page.Content().Column(col =>
            {
                col.Spacing(12);

                // Project summary header
                col.Item().Background(Colors.Grey.Lighten4).Padding(10).Column(c =>
                {
                    c.Item().Text(project.ProjectName).Bold().FontSize(14);
                    c.Item().Text($"{project.DeveloperName}  ·  {project.BankName}")
                        .FontSize(10).FontColor(Colors.Grey.Darken2);
                    c.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text($"Status: ").SemiBold();
                        r.RelativeItem(3).Text(statusLabel).FontColor(statusColor).Bold();
                    });
                });

                // KPIs
                col.Item().Row(r =>
                {
                    r.RelativeItem().Padding(4).Column(c =>
                    {
                        c.Item().Text("Current balance").FontColor(Colors.Grey.Darken1).FontSize(9);
                        c.Item().Text($"AED {project.CurrentBalance:N0}").Bold().FontSize(13);
                    });
                    r.RelativeItem().Padding(4).Column(c =>
                    {
                        c.Item().Text("Required minimum").FontColor(Colors.Grey.Darken1).FontSize(9);
                        c.Item().Text($"AED {project.RequiredMinimumBalance:N0}").Bold().FontSize(13);
                    });
                    r.RelativeItem().Padding(4).Column(c =>
                    {
                        c.Item().Text("Adequacy ratio").FontColor(Colors.Grey.Darken1).FontSize(9);
                        c.Item().Text($"{project.AdequacyRatio:P1}").Bold().FontSize(13).FontColor(statusColor);
                    });
                    r.RelativeItem().Padding(4).Column(c =>
                    {
                        c.Item().Text("Ledger entries (90d)").FontColor(Colors.Grey.Darken1).FontSize(9);
                        c.Item().Text(audit.Count.ToString()).Bold().FontSize(13);
                    });
                });

                // Inflows / outflows summary
                col.Item().Padding(4).Column(c =>
                {
                    c.Item().Text("90-day cash flow").Bold().FontSize(11);
                    c.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text($"Total inflows: AED {deposits:N0}").FontColor(Colors.Green.Darken2);
                        r.RelativeItem().Text($"Total outflows: AED {Math.Abs(withdrawals):N0}").FontColor(Colors.Red.Darken2);
                        r.RelativeItem().Text($"Net: AED {(deposits + withdrawals):N0}").SemiBold();
                    });
                });

                // Red-flag summary
                col.Item().Padding(4).Column(c =>
                {
                    c.Item().Text("Red flags").Bold().FontSize(11);
                    var flags = BuildRedFlags(project);
                    if (flags.Count == 0)
                    {
                        c.Item().Text("• No material red flags this period.").FontColor(Colors.Green.Darken2);
                    }
                    else
                    {
                        foreach (var f in flags)
                            c.Item().Text($"• {f}").FontColor(Colors.Red.Darken2);
                    }
                });

                // Recommended actions (informational only per RFP ESC-003)
                col.Item().Padding(4).Column(c =>
                {
                    c.Item().Text("Recommended actions (informational)").Bold().FontSize(11);
                    foreach (var rec in BuildRecommendations(project))
                        c.Item().Text($"• {rec}");
                });

                // Audit log
                col.Item().Padding(4).Column(c =>
                {
                    c.Item().Text("Audit log (last 90 days)").Bold().FontSize(11);
                    c.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(1.4f);
                            cols.RelativeColumn(1.2f);
                            cols.RelativeColumn(1.1f);
                            cols.RelativeColumn(1.1f);
                            cols.RelativeColumn(1.0f);
                        });
                        string[] headers = { "Timestamp (UTC)", "Type", "Amount (AED)", "Balance after", "Reference" };
                        foreach (var h in headers)
                            t.Cell().Background(Colors.Green.Darken3).Padding(3)
                                .Text(h).FontColor(Colors.White).Bold().FontSize(8);
                        for (var i = 0; i < audit.Count; i++)
                        {
                            var e = audit[i];
                            var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                            t.Cell().Background(bg).Padding(2).Text(e.At.ToString("dd MMM yyyy HH:mm")).FontSize(8);
                            t.Cell().Background(bg).Padding(2).Text(e.Type).FontSize(8);
                            t.Cell().Background(bg).Padding(2).AlignRight()
                                .Text($"{(e.Amount >= 0 ? "+" : "")}{e.Amount:N0}")
                                .FontSize(8).FontColor(e.Amount >= 0 ? Colors.Green.Darken2 : Colors.Red.Darken2);
                            t.Cell().Background(bg).Padding(2).AlignRight().Text($"{e.BalanceAfter:N0}").FontSize(8);
                            t.Cell().Background(bg).Padding(2).Text(e.ReferenceNumber).FontSize(7);
                        }
                    });
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
                text.Span(" | Dubai Land Department — Confidential | Generated ")
                    .FontColor(Colors.Grey.Medium);
                text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")).FontColor(Colors.Grey.Medium);
                text.Span(" UTC").FontColor(Colors.Grey.Medium);
            });
        }));

        var safeName = new string(project.ProjectName.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        return new Report(doc.GeneratePdf(),
            $"EscrowHealth_{safeName}_{DateTime.UtcNow:yyyyMM}.pdf");
    }

    private static List<string> BuildRedFlags(EscrowItem p) => new[]
    {
        p.AdequacyRatio < 0.6m  ? "Adequacy ratio below 60% — triggers Level-2 escalation (RFP ESC-001)." : null,
        p.AdequacyRatio < 0.8m  && p.AdequacyRatio >= 0.6m ? "Adequacy ratio in warning band (60–80%)." : null,
        p.CurrentBalance < p.RequiredMinimumBalance * 0.5m ? "Current balance materially below required minimum." : null,
    }.Where(s => s != null).Cast<string>().ToList();

    private static List<string> BuildRecommendations(EscrowItem p) => p.Status switch
    {
        2 => new()
        {
            "Notify assigned project officer within 1 business hour; prepare Level-3 escalation packet.",
            "Request bank statement and reconciliation schedule from the escrow agent within 24 hours.",
            "Pause any pending milestone releases until adequacy is restored to ≥ 80%."
        },
        1 => new()
        {
            "Monitor daily; email developer requesting replenishment schedule.",
            "Review upcoming milestone releases for this quarter and defer non-critical ones."
        },
        _ => new()
        {
            "No immediate action required — continue standard monthly review cadence."
        }
    };
}
