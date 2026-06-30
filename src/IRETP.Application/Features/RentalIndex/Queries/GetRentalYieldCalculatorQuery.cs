using IRETP.Application.DTOs;
using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.RentalIndex.Queries;

public class GetRentalYieldCalculatorQuery : IRequest<List<RentalYieldCalculationDto>>
{
    public List<Guid>? ZoneIds { get; set; }
    public PropertyType? UnitType { get; set; }
}
