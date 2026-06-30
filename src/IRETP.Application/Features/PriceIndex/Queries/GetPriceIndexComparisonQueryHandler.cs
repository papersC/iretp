using IRETP.Application.DTOs;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.PriceIndex.Queries;

public class GetPriceIndexComparisonQueryHandler
    : IRequestHandler<GetPriceIndexComparisonQuery, PriceIndexComparisonDto>
{
    private readonly IRepository<Domain.Entities.PriceIndex> _priceIndexRepo;

    public GetPriceIndexComparisonQueryHandler(IRepository<Domain.Entities.PriceIndex> priceIndexRepo)
    {
        _priceIndexRepo = priceIndexRepo;
    }

    public async Task<PriceIndexComparisonDto> Handle(
        GetPriceIndexComparisonQuery request, CancellationToken cancellationToken)
    {
        if (request.ZoneIds.Count == 0 || request.ZoneIds.Count > 5)
            return new PriceIndexComparisonDto();

        var currentYear = DateTime.UtcNow.Year;
        var yearFrom = request.YearFrom ?? currentYear - 10;
        var yearTo = request.YearTo ?? currentYear;

        var query = _priceIndexRepo.Query()
            .Include(p => p.Zone)
            .Where(p => request.ZoneIds.Contains(p.ZoneId))
            .Where(p => p.Year >= yearFrom && p.Year <= yearTo);

        if (request.PropertyType.HasValue)
            query = query.Where(p => p.PropertyType == request.PropertyType.Value);

        if (request.IsOffPlan.HasValue)
            query = query.Where(p => p.IsOffPlan == request.IsOffPlan.Value);

        var data = await query
            .OrderBy(p => p.Year).ThenBy(p => p.Quarter)
            .ToListAsync(cancellationToken);

        var grouped = data.GroupBy(p => p.ZoneId);

        var result = new PriceIndexComparisonDto
        {
            ZoneSeries = grouped.Select(g =>
            {
                var latest = g.OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).First();
                return new ZonePriceSeriesDto
                {
                    ZoneId = g.Key,
                    ZoneName = latest.Zone.Name,
                    ZoneNameAr = latest.Zone.NameAr,
                    LatestAvgPrice = latest.AveragePricePerSqft,
                    QuarterlyChange = latest.QuarterlyChange,
                    AnnualChange = latest.AnnualChange,
                    DataPoints = g.Select(p => new PriceIndexDto
                    {
                        ZoneId = p.ZoneId,
                        ZoneName = p.Zone.Name,
                        ZoneNameAr = p.Zone.NameAr,
                        PropertyType = p.PropertyType,
                        IsOffPlan = p.IsOffPlan,
                        Year = p.Year,
                        Quarter = p.Quarter,
                        Month = p.Month,
                        AveragePricePerSqft = p.AveragePricePerSqft,
                        TransactionCount = p.TransactionCount,
                        TotalValue = p.TotalValue,
                        QuarterlyChange = p.QuarterlyChange,
                        AnnualChange = p.AnnualChange
                    }).ToList()
                };
            }).ToList()
        };

        return result;
    }
}
