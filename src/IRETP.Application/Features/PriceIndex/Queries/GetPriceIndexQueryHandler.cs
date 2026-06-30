using IRETP.Application.DTOs;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.PriceIndex.Queries;

public class GetPriceIndexQueryHandler : IRequestHandler<GetPriceIndexQuery, PriceIndexTrendDto>
{
    private readonly IRepository<Domain.Entities.PriceIndex> _priceIndexRepo;

    public GetPriceIndexQueryHandler(IRepository<Domain.Entities.PriceIndex> priceIndexRepo)
    {
        _priceIndexRepo = priceIndexRepo;
    }

    public async Task<PriceIndexTrendDto> Handle(GetPriceIndexQuery request, CancellationToken cancellationToken)
    {
        var query = _priceIndexRepo.Query()
            .Include(p => p.Zone)
            .AsQueryable();

        if (request.ZoneId.HasValue)
            query = query.Where(p => p.ZoneId == request.ZoneId.Value);

        if (request.PropertyType.HasValue)
            query = query.Where(p => p.PropertyType == request.PropertyType.Value);

        if (request.IsOffPlan.HasValue)
            query = query.Where(p => p.IsOffPlan == request.IsOffPlan.Value);

        var currentYear = DateTime.UtcNow.Year;
        var yearFrom = request.YearFrom ?? currentYear - 10;
        var yearTo = request.YearTo ?? currentYear;

        query = query.Where(p => p.Year >= yearFrom && p.Year <= yearTo);

        var data = await query
            .OrderBy(p => p.Year).ThenBy(p => p.Quarter).ThenBy(p => p.Month)
            .ToListAsync(cancellationToken);

        var dataPoints = data.Select(p => new PriceIndexDto
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
        }).ToList();

        var latest = data.LastOrDefault();

        return new PriceIndexTrendDto
        {
            DataPoints = dataPoints,
            CurrentAvgPricePerSqft = latest?.AveragePricePerSqft ?? 0,
            LatestQuarterlyChange = latest?.QuarterlyChange,
            LatestAnnualChange = latest?.AnnualChange
        };
    }
}
