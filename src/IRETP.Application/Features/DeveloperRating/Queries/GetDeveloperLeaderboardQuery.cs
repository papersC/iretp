using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class GetDeveloperLeaderboardQuery : IRequest<List<DeveloperScoreDto>>
{
    public int? Year { get; set; }
    public int? Quarter { get; set; }
    public int? Top { get; set; }
}
