using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Transactions.Queries;

public class GetTransactionsQueryHandler
    : IRequestHandler<GetTransactionsQuery, PagedResult<TransactionDto>>
{
    private readonly IRepository<Transaction> _transactionRepo;

    public GetTransactionsQueryHandler(IRepository<Transaction> transactionRepo)
    {
        _transactionRepo = transactionRepo;
    }

    public async Task<PagedResult<TransactionDto>> Handle(
        GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        var query = _transactionRepo.Query().AsQueryable();

        if (request.DateFrom.HasValue)
            query = query.Where(t => t.TransactionDate >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(t => t.TransactionDate <= request.DateTo.Value);

        if (request.ZoneIds is { Count: > 0 })
            query = query.Where(t => request.ZoneIds.Contains(t.ZoneId));

        if (request.PropertyTypes is { Count: > 0 })
            query = query.Where(t => request.PropertyTypes.Contains(t.PropertyType));

        if (request.TransactionTypes is { Count: > 0 })
            query = query.Where(t => request.TransactionTypes.Contains(t.TransactionType));

        if (request.PriceMin.HasValue)
            query = query.Where(t => t.TransactionValue >= request.PriceMin.Value);

        if (request.PriceMax.HasValue)
            query = query.Where(t => t.TransactionValue <= request.PriceMax.Value);

        if (request.AreaMin.HasValue)
            query = query.Where(t => t.AreaSqft >= request.AreaMin.Value);

        if (request.AreaMax.HasValue)
            query = query.Where(t => t.AreaSqft <= request.AreaMax.Value);

        if (request.FinancingMethod.HasValue)
            query = query.Where(t => t.FinancingMethod == request.FinancingMethod.Value);

        // Sorting
        query = request.SortBy?.ToLowerInvariant() switch
        {
            "date" => request.SortDesc
                ? query.OrderByDescending(t => t.TransactionDate)
                : query.OrderBy(t => t.TransactionDate),
            "value" => request.SortDesc
                ? query.OrderByDescending(t => t.TransactionValue)
                : query.OrderBy(t => t.TransactionValue),
            "pricepersqft" => request.SortDesc
                ? query.OrderByDescending(t => t.PricePerSqft)
                : query.OrderBy(t => t.PricePerSqft),
            "area" => request.SortDesc
                ? query.OrderByDescending(t => t.AreaSqft)
                : query.OrderBy(t => t.AreaSqft),
            _ => query.OrderByDescending(t => t.TransactionDate)
        };

        var totalCount = query.Count();

        var items = query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                TransactionDate = t.TransactionDate,
                ZoneId = t.ZoneId,
                ZoneName = t.Zone.Name,
                Community = t.Community,
                ProjectId = t.ProjectId,
                ProjectName = t.ProjectName,
                PropertyType = t.PropertyType,
                TransactionType = t.TransactionType,
                AreaSqft = t.AreaSqft,
                AreaSqm = t.AreaSqm,
                TransactionValue = t.TransactionValue,
                PricePerSqft = t.PricePerSqft,
                FinancingMethod = t.FinancingMethod,
                IsOffPlan = t.IsOffPlan,
                CreatedAt = t.CreatedAt
            })
            .ToList();

        return await Task.FromResult(
            new PagedResult<TransactionDto>(items, totalCount, request.Page, request.PageSize));
    }
}
