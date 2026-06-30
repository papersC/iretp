using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.Transactions.Queries;

public class GetTransactionsQuery : IRequest<PagedResult<TransactionDto>>
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public List<Guid>? ZoneIds { get; set; }
    public List<PropertyType>? PropertyTypes { get; set; }
    public List<TransactionType>? TransactionTypes { get; set; }
    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public decimal? AreaMin { get; set; }
    public decimal? AreaMax { get; set; }
    public FinancingMethod? FinancingMethod { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}
