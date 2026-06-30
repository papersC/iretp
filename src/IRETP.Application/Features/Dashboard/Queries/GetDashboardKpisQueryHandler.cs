using IRETP.Application.DTOs;
using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Dashboard.Queries;

public class GetDashboardKpisQueryHandler : IRequestHandler<GetDashboardKpisQuery, DashboardKpiDto>
{
    /// <summary>
    /// FR003 mandates KPI freshness no worse than 15 minutes. We treat the
    /// cached snapshot as authoritative within this window so the homepage
    /// doesn't trigger expensive aggregations on every render.
    /// </summary>
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    private readonly IRepository<Transaction> _transactionRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<Domain.Entities.RentalIndex> _rentalRepo;
    private readonly IKpiSnapshotCache _cache;

    public GetDashboardKpisQueryHandler(
        IRepository<Transaction> transactionRepo,
        IRepository<Project> projectRepo,
        IRepository<Domain.Entities.RentalIndex> rentalRepo,
        IKpiSnapshotCache cache)
    {
        _transactionRepo = transactionRepo;
        _projectRepo = projectRepo;
        _rentalRepo = rentalRepo;
        _cache = cache;
    }

    public async Task<DashboardKpiDto> Handle(
        GetDashboardKpisQuery request, CancellationToken cancellationToken)
    {
        if (!request.ForceRefresh)
        {
            var cached = _cache.Current;
            if (cached is not null && DateTime.UtcNow - cached.RefreshedAt < CacheTtl)
            {
                return cached;
            }
        }
        var now = DateTime.UtcNow;
        var startOfYear = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfPrevMonth = startOfMonth.AddMonths(-1);
        var startOfPrevYear = startOfYear.AddYears(-1);
        var endOfPrevYear = startOfYear.AddTicks(-1);

        var allTransactions = _transactionRepo.Query();

        // Current period metrics
        var ytdTransactions = allTransactions.Where(t => t.TransactionDate >= startOfYear);
        var currentMonthTx = allTransactions.Where(t => t.TransactionDate >= startOfMonth);
        var prevMonthTx = allTransactions.Where(t =>
            t.TransactionDate >= startOfPrevMonth && t.TransactionDate < startOfMonth);
        var prevYearTx = allTransactions.Where(t =>
            t.TransactionDate >= startOfPrevYear && t.TransactionDate <= endOfPrevYear);

        var totalCount = ytdTransactions.Count();
        var totalValue = ytdTransactions.Any() ? ytdTransactions.Sum(t => t.TransactionValue) : 0m;
        var avgPricePerSqft = ytdTransactions.Any() ? ytdTransactions.Average(t => t.PricePerSqft) : 0m;

        // Month-over-month trends
        var currentMonthCount = currentMonthTx.Count();
        var prevMonthCount = prevMonthTx.Count();
        var countMoM = prevMonthCount > 0
            ? (decimal)(currentMonthCount - prevMonthCount) / prevMonthCount * 100
            : (decimal?)null;

        var currentMonthValue = currentMonthTx.Any() ? currentMonthTx.Sum(t => t.TransactionValue) : 0m;
        var prevMonthValue = prevMonthTx.Any() ? prevMonthTx.Sum(t => t.TransactionValue) : 0m;
        var valueMoM = prevMonthValue > 0
            ? (currentMonthValue - prevMonthValue) / prevMonthValue * 100
            : (decimal?)null;

        var currentMonthAvgPrice = currentMonthTx.Any() ? currentMonthTx.Average(t => t.PricePerSqft) : 0m;
        var prevMonthAvgPrice = prevMonthTx.Any() ? prevMonthTx.Average(t => t.PricePerSqft) : 0m;
        var priceMoM = prevMonthAvgPrice > 0
            ? (currentMonthAvgPrice - prevMonthAvgPrice) / prevMonthAvgPrice * 100
            : (decimal?)null;

        // Year-over-year trends
        var prevYearCount = prevYearTx.Count();
        var countYoY = prevYearCount > 0
            ? (decimal)(totalCount - prevYearCount) / prevYearCount * 100
            : (decimal?)null;

        var prevYearValue = prevYearTx.Any() ? prevYearTx.Sum(t => t.TransactionValue) : 0m;
        var valueYoY = prevYearValue > 0
            ? (totalValue - prevYearValue) / prevYearValue * 100
            : (decimal?)null;

        var prevYearAvgPrice = prevYearTx.Any() ? prevYearTx.Average(t => t.PricePerSqft) : 0m;
        var priceYoY = prevYearAvgPrice > 0
            ? (avgPricePerSqft - prevYearAvgPrice) / prevYearAvgPrice * 100
            : (decimal?)null;

        // Completed units YTD
        var projects = _projectRepo.Query();
        var completedUnitsYtd = projects
            .Where(p => p.Status == ProjectStatus.Completed
                        && p.ActualDeliveryDate.HasValue
                        && p.ActualDeliveryDate.Value >= startOfYear)
            .Sum(p => p.TotalUnits);

        // Active off-plan projects
        var activeOffPlan = projects
            .Count(p => p.Status == ProjectStatus.UnderConstruction
                        || p.Status == ProjectStatus.FutureAnnounced);

        // Average rental yield (latest quarter)
        var rentalIndices = _rentalRepo.Query();
        var latestRental = rentalIndices
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Quarter)
            .FirstOrDefault();

        var avgRentalYield = 0m;
        if (latestRental != null)
        {
            avgRentalYield = rentalIndices
                .Where(r => r.Year == latestRental.Year && r.Quarter == latestRental.Quarter)
                .Average(r => r.GrossRentalYield);
        }

        var snapshot = new DashboardKpiDto
        {
            TotalTransactionsCount = new KpiMetric
            {
                Value = totalCount,
                TrendMoM = countMoM,
                TrendYoY = countYoY
            },
            TotalTransactionsValue = new KpiMetric
            {
                Value = totalValue,
                TrendMoM = valueMoM,
                TrendYoY = valueYoY
            },
            AveragePricePerSqft = new KpiMetric
            {
                Value = avgPricePerSqft,
                TrendMoM = priceMoM,
                TrendYoY = priceYoY
            },
            CompletedUnitsYtd = new KpiMetric
            {
                Value = completedUnitsYtd
            },
            ActiveOffPlanProjects = new KpiMetric
            {
                Value = activeOffPlan
            },
            AverageRentalYield = new KpiMetric
            {
                Value = avgRentalYield
            }
        };

        _cache.Set(snapshot);
        return await Task.FromResult(snapshot);
    }
}
