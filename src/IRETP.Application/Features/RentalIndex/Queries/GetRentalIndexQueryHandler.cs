using IRETP.Application.DTOs;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.RentalIndex.Queries;

public class GetRentalIndexQueryHandler : IRequestHandler<GetRentalIndexQuery, RentalIndexTrendDto>
{
    private readonly IRepository<Domain.Entities.RentalIndex> _rentalRepo;

    public GetRentalIndexQueryHandler(IRepository<Domain.Entities.RentalIndex> rentalRepo)
    {
        _rentalRepo = rentalRepo;
    }

    public async Task<RentalIndexTrendDto> Handle(GetRentalIndexQuery request, CancellationToken cancellationToken)
    {
        var query = _rentalRepo.Query()
            .Include(r => r.Zone)
            .AsQueryable();

        if (request.ZoneId.HasValue)
            query = query.Where(r => r.ZoneId == request.ZoneId.Value);

        if (request.UnitType.HasValue)
            query = query.Where(r => r.UnitType == request.UnitType.Value);

        if (request.IsShortTerm.HasValue)
            query = query.Where(r => r.IsShortTerm == request.IsShortTerm.Value);

        var currentYear = DateTime.UtcNow.Year;
        var yearFrom = request.YearFrom ?? currentYear - 5;
        var yearTo = request.YearTo ?? currentYear;

        query = query.Where(r => r.Year >= yearFrom && r.Year <= yearTo);

        var data = await query
            .OrderBy(r => r.Year).ThenBy(r => r.Quarter)
            .ToListAsync(cancellationToken);

        var dataPoints = data.Select(r => new RentalIndexDto
        {
            ZoneId = r.ZoneId,
            ZoneName = r.Zone.Name,
            ZoneNameAr = r.Zone.NameAr,
            UnitType = r.UnitType,
            IsShortTerm = r.IsShortTerm,
            Year = r.Year,
            Quarter = r.Quarter,
            AverageAnnualRent = r.AverageAnnualRent,
            GrossRentalYield = r.GrossRentalYield,
            SampleSize = r.SampleSize
        }).ToList();

        var latest = data.LastOrDefault();

        return new RentalIndexTrendDto
        {
            DataPoints = dataPoints,
            CurrentAvgRent = latest?.AverageAnnualRent ?? 0,
            CurrentAvgYield = latest?.GrossRentalYield ?? 0
        };
    }
}
