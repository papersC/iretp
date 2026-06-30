using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Map.Queries;

public class GetZoneHeatmapQueryHandler
    : IRequestHandler<GetZoneHeatmapQuery, List<ZoneHeatmapDto>>
{
    private readonly IRepository<Zone> _zoneRepo;
    private readonly IRepository<Transaction> _transactionRepo;
    private readonly IRepository<Domain.Entities.RentalIndex> _rentalRepo;

    public GetZoneHeatmapQueryHandler(
        IRepository<Zone> zoneRepo,
        IRepository<Transaction> transactionRepo,
        IRepository<Domain.Entities.RentalIndex> rentalRepo)
    {
        _zoneRepo = zoneRepo;
        _transactionRepo = transactionRepo;
        _rentalRepo = rentalRepo;
    }

    public async Task<List<ZoneHeatmapDto>> Handle(
        GetZoneHeatmapQuery request, CancellationToken cancellationToken)
    {
        var zones = _zoneRepo.Query().ToList();
        var transactions = _transactionRepo.Query();
        var rentals = _rentalRepo.Query();

        // Latest rental quarter
        var latestRental = rentals
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Quarter)
            .FirstOrDefault();

        var result = new List<ZoneHeatmapDto>();

        foreach (var zone in zones)
        {
            var zoneTx = transactions.Where(t => t.ZoneId == zone.Id);
            var txCount = zoneTx.Count();
            var avgPrice = txCount > 0 ? zoneTx.Average(t => t.PricePerSqft) : 0m;

            var avgYield = 0m;
            if (latestRental != null)
            {
                var zoneRentals = rentals
                    .Where(r => r.ZoneId == zone.Id
                                && r.Year == latestRental.Year
                                && r.Quarter == latestRental.Quarter);

                if (zoneRentals.Any())
                    avgYield = zoneRentals.Average(r => r.GrossRentalYield);
            }

            result.Add(new ZoneHeatmapDto
            {
                ZoneId = zone.Id,
                Name = zone.Name,
                NameAr = zone.NameAr,
                CenterLat = zone.CenterLat,
                CenterLng = zone.CenterLng,
                GeoJson = zone.GeoJson,
                TransactionCount = txCount,
                AvgPricePerSqft = avgPrice,
                AvgRentalYield = avgYield
            });
        }

        return await Task.FromResult(result);
    }
}
