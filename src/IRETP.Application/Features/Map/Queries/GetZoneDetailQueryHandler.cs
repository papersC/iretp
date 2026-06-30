using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Enums;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Map.Queries;

public class GetZoneDetailQueryHandler : IRequestHandler<GetZoneDetailQuery, ZoneDetailDto?>
{
    private readonly IRepository<Zone> _zoneRepo;
    private readonly IRepository<Transaction> _transactionRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<Domain.Entities.RentalIndex> _rentalRepo;

    public GetZoneDetailQueryHandler(
        IRepository<Zone> zoneRepo,
        IRepository<Transaction> transactionRepo,
        IRepository<Project> projectRepo,
        IRepository<Domain.Entities.RentalIndex> rentalRepo)
    {
        _zoneRepo = zoneRepo;
        _transactionRepo = transactionRepo;
        _projectRepo = projectRepo;
        _rentalRepo = rentalRepo;
    }

    public async Task<ZoneDetailDto?> Handle(GetZoneDetailQuery request, CancellationToken cancellationToken)
    {
        var zone = await _zoneRepo.GetByIdAsync(request.ZoneId, cancellationToken);
        if (zone == null) return null;

        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);

        var transactions = await _transactionRepo.Query()
            .Where(t => t.ZoneId == request.ZoneId && t.TransactionDate >= twelveMonthsAgo)
            .ToListAsync(cancellationToken);

        var totalTransactions = transactions.Count;
        var sales = transactions.Where(t => t.TransactionType == TransactionType.Sale).ToList();
        var avgSalePricePerSqft = sales.Count > 0 ? sales.Average(t => t.PricePerSqft) : 0;

        // Rental data - latest quarter
        var latestRental = await _rentalRepo.Query()
            .Where(r => r.ZoneId == request.ZoneId)
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Quarter)
            .FirstOrDefaultAsync(cancellationToken);

        var rentalsByType = latestRental != null
            ? await _rentalRepo.Query()
                .Where(r => r.ZoneId == request.ZoneId
                    && r.Year == latestRental.Year
                    && r.Quarter == latestRental.Quarter)
                .ToListAsync(cancellationToken)
            : new List<Domain.Entities.RentalIndex>();

        var avgGrossYield = rentalsByType.Count > 0
            ? rentalsByType.Average(r => r.GrossRentalYield) : 0;

        // Price trend
        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var recentSales = transactions.Where(t => t.TransactionDate >= threeMonthsAgo).ToList();
        var priorSales = transactions.Where(t => t.TransactionDate >= sixMonthsAgo && t.TransactionDate < threeMonthsAgo).ToList();

        var recentAvg = recentSales.Count > 0 ? recentSales.Average(t => t.PricePerSqft) : 0;
        var priorAvg = priorSales.Count > 0 ? priorSales.Average(t => t.PricePerSqft) : 0;

        string priceTrend = "stable";
        decimal priceTrendPct = 0;
        if (priorAvg > 0)
        {
            priceTrendPct = (recentAvg - priorAvg) / priorAvg * 100;
            priceTrend = priceTrendPct > 2 ? "up" : priceTrendPct < -2 ? "down" : "stable";
        }

        // Top 3 developers
        var topDevelopers = await _projectRepo.Query()
            .Include(p => p.Developer)
            .Where(p => p.ZoneId == request.ZoneId
                && (p.Status == ProjectStatus.UnderConstruction || p.Status == ProjectStatus.Completed))
            .GroupBy(p => new { p.DeveloperId, p.Developer.Name, p.Developer.NameAr })
            .Select(g => new TopDeveloperInfo
            {
                Name = g.Key.Name,
                NameAr = g.Key.NameAr,
                ProjectCount = g.Count()
            })
            .OrderByDescending(g => g.ProjectCount)
            .Take(3)
            .ToListAsync(cancellationToken);

        var activeOffPlan = await _projectRepo.CountAsync(
            p => p.ZoneId == request.ZoneId
                && (p.Status == ProjectStatus.UnderConstruction || p.Status == ProjectStatus.FutureAnnounced),
            cancellationToken);

        var completedCount = await _projectRepo.CountAsync(
            p => p.ZoneId == request.ZoneId && p.Status == ProjectStatus.Completed,
            cancellationToken);

        return new ZoneDetailDto
        {
            ZoneId = zone.Id,
            ZoneName = zone.Name,
            ZoneNameAr = zone.NameAr,
            TotalTransactions12Months = totalTransactions,
            AverageSalePricePerSqft = avgSalePricePerSqft,
            AverageRentByUnitType = rentalsByType.ToDictionary(
                r => r.UnitType.ToString(),
                r => r.AverageAnnualRent),
            AverageGrossRentalYield = avgGrossYield,
            PriceTrend = priceTrend,
            PriceTrendPercentage = priceTrendPct,
            TopDevelopers = topDevelopers,
            ActiveOffPlanProjects = activeOffPlan,
            CompletedProjects = completedCount
        };
    }
}
