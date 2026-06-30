using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class CompareDevelopersQuery : IRequest<DeveloperComparisonDto>
{
    public List<Guid> Ids { get; set; } = [];
}
