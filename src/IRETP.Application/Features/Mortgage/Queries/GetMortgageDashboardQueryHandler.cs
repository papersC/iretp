using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Mortgage.Queries;

/// <summary>
/// Aggregates mortgage and debt-finance transparency metrics from the DLD
/// transaction registry (RFP Section 20). The "LTV proxy" metric expresses
/// registered mortgage value as a share of registered sale value per zone —
/// the closest signal to a true LTV available from public DLD data alone.
/// </summary>
public class GetMortgageDashboardQueryHandler
    : IRequestHandler<GetMortgageDashboardQuery, MortgageDashboardDto>
{
    private readonly IRepository<Transaction> _transactionRepo;

    public GetMortgageDashboardQueryHandler(IRepository<Transaction> transactionRepo)
    {
        _transactionRepo = transactionRepo;
    }

    public async Task<MortgageDashboardDto> Handle(
        GetMortgageDashboardQuery request, CancellationToken ct)
    {
        var lookback = request.LookbackMonths ?? 24;
        var cutoff = DateTime.UtcNow.AddMonths(-lookback);

        var all = await _transactionRepo.Query()
            .Where(t => t.TransactionDate >= cutoff)
            .Include(t => t.Zone)
            .ToListAsync(ct);

        var mortgages = all
            .Where(t => t.TransactionType == TransactionType.Mortgage
                        || t.FinancingMethod == FinancingMethod.Mortgage)
            .ToList();

        var totalRegValue = mortgages.Sum(t => t.TransactionValue);
        var totalAllValue = all.Sum(t => t.TransactionValue);
        var share = totalAllValue == 0 ? 0m
            : Math.Round(totalRegValue / totalAllValue * 100m, 2);

        // Month-over-month value change — compare the last full month to the
        // preceding month so users see a recognisable rolling indicator.
        var trend = mortgages
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new MortgageMonthPoint
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Label = $"{g.Key.Year:0000}-{g.Key.Month:00}",
                Count = g.Count(),
                Value = g.Sum(t => t.TransactionValue)
            })
            .OrderBy(p => p.Year).ThenBy(p => p.Month)
            .ToList();

        decimal momChange = 0m;
        if (trend.Count >= 2)
        {
            var lastTwo = trend.TakeLast(2).ToList();
            var prev = lastTwo[0].Value;
            var curr = lastTwo[1].Value;
            momChange = prev == 0 ? 0m : Math.Round((curr - prev) / prev * 100m, 2);
        }

        var byZone = mortgages
            .GroupBy(t => new { t.ZoneId, ZoneName = t.Zone?.Name ?? "—" })
            .Select(g =>
            {
                var mortgageValue = g.Sum(t => t.TransactionValue);
                var allZoneValue = all
                    .Where(t => t.ZoneId == g.Key.ZoneId)
                    .Sum(t => t.TransactionValue);

                var salesValueInZone = all
                    .Where(t => t.ZoneId == g.Key.ZoneId && t.TransactionType == TransactionType.Sale)
                    .Sum(t => t.TransactionValue);

                return new MortgageZoneItem
                {
                    ZoneId = g.Key.ZoneId,
                    ZoneName = g.Key.ZoneName,
                    MortgageCount = g.Count(),
                    MortgageValue = mortgageValue,
                    MortgageSharePct = allZoneValue == 0 ? 0m
                        : Math.Round(mortgageValue / allZoneValue * 100m, 2),
                    AverageLtvProxyPct = salesValueInZone == 0 ? 0m
                        : Math.Round(mortgageValue / salesValueInZone * 100m, 2)
                };
            })
            .OrderByDescending(z => z.MortgageValue)
            .Take(25)
            .ToList();

        var byPropertyType = mortgages
            .GroupBy(t => t.PropertyType.ToString())
            .Select(g => new MortgagePropertyTypeItem
            {
                PropertyType = g.Key,
                Count = g.Count(),
                Value = g.Sum(t => t.TransactionValue)
            })
            .OrderByDescending(x => x.Value)
            .ToList();

        return new MortgageDashboardDto
        {
            TotalMortgageRecords = mortgages.Count,
            TotalRegisteredValueAed = totalRegValue,
            MortgageValueShareOfAllTransactionsPct = share,
            AverageMortgageValueAed = mortgages.Count == 0 ? 0m
                : Math.Round(mortgages.Average(m => m.TransactionValue), 0),
            MomValueChangePct = momChange,
            ByZone = byZone,
            Trend = trend,
            ByPropertyType = byPropertyType
        };
    }
}
