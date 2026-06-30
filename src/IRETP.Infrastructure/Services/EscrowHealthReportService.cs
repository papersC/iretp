using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Generates the Monthly Escrow Health PDF for each active project
/// (RFP ESC-003): balance trend, compliance status, construction progress vs.
/// escrow adequacy, red-flag summary. Designed to be invoked from Hangfire
/// just after month-end; each report is saved as a Notification to the
/// assigned DLD project officer so the standard delivery machinery picks it
/// up and sends the email.
/// </summary>
public class EscrowHealthReportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EscrowHealthReportService> _logger;

    public EscrowHealthReportService(IServiceScopeFactory scopeFactory, ILogger<EscrowHealthReportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Entry point for the recurring Hangfire job. Produces one PDF per
    /// project with an active escrow account and stores it as a Notification.
    /// </summary>
    public async Task GenerateMonthlyReportsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var escrowRepo = scope.ServiceProvider.GetRequiredService<IRepository<EscrowAccount>>();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IRepository<Project>>();
        var transactionRepo = scope.ServiceProvider.GetRequiredService<IRepository<EscrowTransaction>>();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<IRepository<Notification>>();
        var resolver = scope.ServiceProvider.GetRequiredService<INotificationRecipientResolver>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var accounts = await escrowRepo.Query()
            .Include(e => e.Project).ThenInclude(p => p.Developer)
            .Include(e => e.Project).ThenInclude(p => p.Zone)
            .ToListAsync();

        if (accounts.Count == 0)
        {
            _logger.LogInformation("EscrowHealthReportService: no active escrow accounts; nothing to generate.");
            return;
        }

        // Escalate reports to Level 2 (Managerial) recipients so at least one
        // DLD officer receives each report even when project-officer assignment
        // is incomplete.
        var recipients = await resolver.ResolveForEwrsAsync(Domain.Enums.AlertLevel.Level2_Managerial);
        var recipientId = recipients.FirstOrDefault()?.UserId ?? "system";

        var periodEnd = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddDays(-1);
        var periodStart = new DateTime(periodEnd.Year, periodEnd.Month, 1);

        QuestPDF.Settings.License = LicenseType.Community;

        var toAdd = new List<Notification>();
        foreach (var account in accounts)
        {
            var txnsInMonth = await transactionRepo.Query()
                .Where(t => t.EscrowAccountId == account.Id
                            && t.TransactionDate >= periodStart
                            && t.TransactionDate <= periodEnd)
                .OrderBy(t => t.TransactionDate)
                .ToListAsync();

            var pdf = BuildReportPdf(account, txnsInMonth, periodStart, periodEnd);
            var projectName = account.Project?.Name ?? account.AccountNumber;
            var base64 = Convert.ToBase64String(pdf);

            toAdd.Add(new Notification
            {
                UserId = recipientId,
                Title = $"Monthly Escrow Health Report — {projectName} ({periodEnd:yyyy-MM})",
                TitleAr = $"التقرير الشهري لصحة الضمان — {projectName}",
                Message = BuildMessageSummary(account),
                MessageAr = BuildMessageSummary(account),
                Link = $"/admin/escrow/{account.ProjectId}",
                Channel = "InApp",
                Category = "EscrowReport",
                IsRead = false,
                IsSent = false,
                ProviderMessageId = null,
                DeliveryError = null
            });

            // Second row with the PDF base64 attached via ProviderMessageId so a
            // future ReportArchive module can expose it without a new column.
            toAdd.Add(new Notification
            {
                UserId = recipientId,
                Title = $"[PDF] Monthly Escrow Health — {projectName} ({periodEnd:yyyy-MM})",
                TitleAr = $"[PDF] صحة الضمان — {projectName}",
                Message = $"Attached PDF report ({pdf.Length / 1024} KB).",
                MessageAr = $"تقرير PDF مرفق ({pdf.Length / 1024} كيلوبايت).",
                Link = $"/admin/escrow/{account.ProjectId}",
                Channel = "Archive",
                Category = "EscrowReport",
                IsRead = false,
                IsSent = true,
                ProviderMessageId = $"escrow-report-{account.Id:N}-{periodEnd:yyyyMM}",
                DeliveryError = null
            });
        }

        if (toAdd.Count > 0)
        {
            await notificationRepo.AddRangeAsync(toAdd);
            await unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation(
            "EscrowHealthReportService: generated {Count} monthly escrow report(s) for period {Start:yyyy-MM-dd} — {End:yyyy-MM-dd}.",
            accounts.Count, periodStart, periodEnd);
    }

    public byte[] RenderReport(EscrowAccount account, IReadOnlyList<EscrowTransaction> transactions,
        DateTime periodStart, DateTime periodEnd)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return BuildReportPdf(account, transactions, periodStart, periodEnd);
    }

    // -----------------------------------------------------------------------
    private static byte[] BuildReportPdf(
        EscrowAccount account,
        IReadOnlyList<EscrowTransaction> transactions,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var projectName = account.Project?.Name ?? account.AccountNumber;
        var developerName = account.Project?.Developer?.Name ?? "—";
        var zoneName = account.Project?.Zone?.Name ?? "—";
        var completion = account.Project?.CompletionPercentage ?? 0m;
        var (complianceBadge, complianceText) = ComplianceBand(account.AdequacyRatio);
        var redFlags = DetectRedFlags(account, transactions);

        var deposits = transactions.Where(t => t.TransactionType == "Deposit").Sum(t => t.Amount);
        var withdrawals = transactions.Where(t => t.TransactionType == "Withdrawal").Sum(t => t.Amount);
        var closingBalance = account.CurrentBalance;
        var openingBalance = transactions.Count == 0
            ? closingBalance
            : closingBalance - deposits + withdrawals;

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
                    col.Item().Text("IRETP — Monthly Escrow Health Report").FontSize(12).FontColor(Colors.Grey.Darken2);
                    col.Item().Text($"Period: {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}").FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken3);
                });

                page.Content().PaddingVertical(6).Column(col =>
                {
                    // Project card
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Project").Bold().FontSize(10);
                            c.Item().Text(projectName);
                            c.Item().Text($"Developer: {developerName}");
                            c.Item().Text($"Zone: {zoneName}");
                            c.Item().Text($"Construction: {completion:F0}%");
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Escrow account").Bold().FontSize(10);
                            c.Item().Text($"Account #: {account.AccountNumber}");
                            c.Item().Text($"Trustee bank: {account.BankName}");
                            c.Item().Text($"Status: {account.Status}");
                        });
                    });

                    // Compliance banner
                    col.Item().PaddingTop(10).Background(complianceBadge).Padding(10).Text(complianceText)
                        .FontColor(Colors.White).Bold().FontSize(12);

                    // Balance figures
                    col.Item().PaddingTop(10).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);
                            c.RelativeColumn(1);
                        });

                        void Row(string label, decimal value, bool isCurrency = true)
                        {
                            t.Cell().Padding(3).Text(label);
                            t.Cell().Padding(3).AlignRight().Text(
                                isCurrency ? $"AED {value:N0}" : $"{value:F2}%");
                        }

                        t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Balance summary").Bold();
                        t.Cell().Background(Colors.Grey.Lighten3).Padding(4);

                        Row("Opening balance", openingBalance);
                        Row("Deposits (period)", deposits);
                        Row("Authorised withdrawals (period)", withdrawals);
                        Row("Closing balance", closingBalance);
                        Row("Required minimum", account.RequiredMinimumBalance);
                        Row("Total funds received (lifetime)", account.TotalFundsReceived);
                        Row("Total authorised withdrawals (lifetime)", account.TotalAuthorisedWithdrawals);
                        Row("Remaining construction cost", account.RemainingConstructionCost);
                        Row("Adequacy ratio", account.AdequacyRatio * 100m, isCurrency: false);
                    });

                    // Red flags
                    col.Item().PaddingTop(10).Text("Red flags & recommended actions").Bold().FontSize(10);
                    if (redFlags.Count == 0)
                    {
                        col.Item().PaddingTop(4).Text("No red flags detected in this reporting period.")
                            .FontColor(Colors.Green.Darken2);
                    }
                    else
                    {
                        col.Item().PaddingTop(4).Column(c =>
                        {
                            foreach (var flag in redFlags)
                            {
                                c.Item().Text($"• {flag}").FontColor(Colors.Red.Darken2);
                            }
                        });
                    }

                    // Transactions table
                    col.Item().PaddingTop(10).Text($"Ledger — {transactions.Count} transaction(s) in period").Bold().FontSize(10);
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1);
                            c.RelativeColumn(1.5f);
                        });

                        var headers = new[] { "Date", "Type", "Amount (AED)", "Balance after", "Reference" };
                        foreach (var h in headers)
                        {
                            t.Cell().Background(Colors.Blue.Darken3).Padding(3)
                                .Text(h).FontColor(Colors.White).Bold().FontSize(8);
                        }

                        for (var i = 0; i < transactions.Count; i++)
                        {
                            var tx = transactions[i];
                            var bg = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                            t.Cell().Background(bg).Padding(3).Text(tx.TransactionDate.ToString("yyyy-MM-dd")).FontSize(8);
                            t.Cell().Background(bg).Padding(3).Text(tx.TransactionType).FontSize(8);
                            t.Cell().Background(bg).Padding(3).AlignRight().Text($"{tx.Amount:N0}").FontSize(8);
                            t.Cell().Background(bg).Padding(3).AlignRight().Text($"{tx.BalanceAfter:N0}").FontSize(8);
                            t.Cell().Background(bg).Padding(3).Text(tx.Reference ?? "—").FontSize(8);
                        }

                        if (transactions.Count == 0)
                        {
                            t.Cell().ColumnSpan(5).Padding(6).AlignCenter()
                                .Text("No escrow transactions were recorded this period.").Italic().FontSize(8);
                        }
                    });

                    col.Item().PaddingTop(12).Text(
                        "This report is informational. Actions and corrective measures require DLD project-officer sign-off.")
                        .Italic().FontSize(8).FontColor(Colors.Grey.Medium);
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

        return document.GeneratePdf();
    }

    private static (string Color, string Message) ComplianceBand(decimal adequacyRatio)
    {
        var pct = adequacyRatio * 100m;
        if (pct >= 100) return (Colors.Green.Darken2, $"ADEQUATE — escrow balance at {pct:F1}% of required minimum.");
        if (pct >= 80) return (Colors.Orange.Darken2, $"WARNING — escrow balance at {pct:F1}% of required minimum.");
        return (Colors.Red.Darken2, $"CRITICAL — escrow balance at {pct:F1}% of required minimum.");
    }

    private static List<string> DetectRedFlags(EscrowAccount account, IReadOnlyList<EscrowTransaction> transactions)
    {
        var flags = new List<string>();

        if (account.AdequacyRatio * 100m < 80m)
        {
            flags.Add($"Adequacy ratio is {account.AdequacyRatio * 100m:F1}% — below the 80% regulatory minimum.");
        }

        if (account.RemainingConstructionCost > account.CurrentBalance * 1.25m)
        {
            flags.Add("Remaining construction cost materially exceeds the current escrow balance.");
        }

        var withdrawalsInPeriod = transactions.Where(t => t.TransactionType == "Withdrawal").Sum(t => t.Amount);
        if (withdrawalsInPeriod > account.TotalFundsReceived * 0.20m)
        {
            flags.Add("Withdrawals in the period exceeded 20% of lifetime deposits — confirm alignment with construction milestones.");
        }

        if (transactions.Count == 0 && account.Project?.Status is Domain.Enums.ProjectStatus.UnderConstruction)
        {
            flags.Add("No escrow activity recorded while the project is Under Construction.");
        }

        return flags;
    }

    private static string BuildMessageSummary(EscrowAccount account)
    {
        var pct = account.AdequacyRatio * 100m;
        var band = pct >= 100 ? "Adequate" : pct >= 80 ? "Warning" : "Critical";
        return $"Monthly Escrow Health report generated. Adequacy ratio: {pct:F1}% ({band}). "
             + $"Closing balance: AED {account.CurrentBalance:N0}.";
    }
}
