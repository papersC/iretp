using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class GetPublicDeveloperScorecardsQuery : IRequest<List<PublicDeveloperScorecardDto>>
{
}
