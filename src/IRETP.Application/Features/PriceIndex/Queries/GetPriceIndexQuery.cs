using IRETP.Application.DTOs;
using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.PriceIndex.Queries;

public class GetPriceIndexQuery : IRequest<PriceIndexTrendDto>
{
    public Guid? ZoneId { get; set; }
    public PropertyType? PropertyType { get; set; }
    public bool? IsOffPlan { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
}
