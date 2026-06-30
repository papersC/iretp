using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Map.Queries;

public class GetZoneDetailQuery : IRequest<ZoneDetailDto?>
{
    public Guid ZoneId { get; set; }
}
