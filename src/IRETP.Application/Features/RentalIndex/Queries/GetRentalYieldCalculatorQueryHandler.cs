using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.RentalIndex.Queries;

public class GetRentalYieldCalculatorQueryHandler
    : IRequestHandler<GetRentalYieldCalculatorQuery, List<RentalYieldCalculationDto>>
{
    private readonly IRepository<Domain.Entities.RentalIndex> _rentalRepo;
    private readonly IRepository<Transaction> _transactionRepo;
    private readonly IRepository<Zone> _zoneRepo;

    public GetRentalYieldCalculatorQueryHandler(
        IRepository<Domain.Entities.RentalIndex> rentalRepo,
        IRepository<Transaction> transactionRepo,
        IRepository<Zone> zoneRepo)
    {
        _rentalRepo = rentalRepo;
        _transactionRepo = transactionRepo;
        _zoneRepo = zoneRepo;
    }

    public async Task<List<RentalYieldCalculationDto>> Handle(
        GetRentalYieldCalculatorQuery request, CancellationToken cancellationToken)
    {
        // Get latest rental data
        var rentalQuery = _rentalRepo.Query().Include(r => r.Zone).AsQueryable();

        if (request.ZoneIds != null && request.ZoneIds.Count > 0)
            rentalQuery = rentalQuery.Where(r => request.ZoneIds.Contains(r.ZoneId));

        if (request.UnitType.HasValue)
            rentalQuery = rentalQuery.Where(r => r.UnitType == request.UnitType.Value);

        // Get only the latest quarter
        var latestEntry = await rentalQuery
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Quarter)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestEntry == null)
            return [];

        var latestRentals = await rentalQuery
            .Where(r => r.Year == latestEntry.Year && r.Quarter == latestEntry.Quarter)
            .ToListAsync(cancellationToken);

        // Get avg transaction prices for matching zones in last 12 months
        var oneYearAgo = DateTime.UtcNow.AddYears(-1);
        var zoneIds = latestRentals.Select(r => r.ZoneId).Distinct().ToList();

        var avgPrices = await _transactionRepo.Query()
            .Where(t => zoneIds.Contains(t.ZoneId) && t.TransactionDate >= oneYearAgo)
            .GroupBy(t => new { t.ZoneId, t.PropertyType })
            .Select(g => new
            {
                g.Key.ZoneId,
                g.Key.PropertyType,
                AvgPrice = g.Average(t => t.TransactionValue)
            })
            .ToListAsync(cancellationToken);

        var results = new List<RentalYieldCalculationDto>();

        foreach (var rental in latestRentals)
        {
            var avgPrice = avgPrices
                .FirstOrDefault(p => p.ZoneId == rental.ZoneId && p.PropertyType == rental.UnitType);

            var transactionPrice = avgPrice?.AvgPrice ?? 0;
            var yield = transactionPrice > 0
                ? (rental.AverageAnnualRent / transactionPrice) * 100
                : rental.GrossRentalYield;

            results.Add(new RentalYieldCalculationDto
            {
                ZoneId = rental.ZoneId,
                ZoneName = rental.Zone.Name,
                ZoneNameAr = rental.Zone.NameAr,
                UnitType = rental.UnitType,
                AverageAnnualRent = rental.AverageAnnualRent,
                AverageTransactionPrice = transactionPrice,
                GrossRentalYield = yield
            });
        }

        return results;
    }
}
