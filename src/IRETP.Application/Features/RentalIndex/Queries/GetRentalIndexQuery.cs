using IRETP.Application.DTOs;
using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.RentalIndex.Queries;

public class GetRentalIndexQuery : IRequest<RentalIndexTrendDto>
{
    public Guid? ZoneId { get; set; }
    public PropertyType? UnitType { get; set; }
    public bool? IsShortTerm { get; set; }
    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }
}
