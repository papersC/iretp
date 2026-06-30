using IRETP.Application.DTOs;
using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.PriceIndex.Queries;

public class GetPriceIndexComparisonQuery : IRequest<PriceIndexComparisonDto>
{
    public List<Guid> ZoneIds { get; set; } = [];
    public PropertyType? PropertyType { get; set; }
    public bool? IsOffPlan { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
}
