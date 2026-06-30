using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class GetScoringWeightsQuery : IRequest<List<ScoringWeightDto>>
{
}
