using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class GetScoringWeightsQueryHandler
    : IRequestHandler<GetScoringWeightsQuery, List<ScoringWeightDto>>
{
    private readonly IRepository<ScoringWeight> _weightRepo;

    public GetScoringWeightsQueryHandler(IRepository<ScoringWeight> weightRepo)
    {
        _weightRepo = weightRepo;
    }

    public async Task<List<ScoringWeightDto>> Handle(
        GetScoringWeightsQuery request, CancellationToken cancellationToken)
    {
        var weights = await _weightRepo.GetAllAsync(cancellationToken);

        return weights.Select(w => new ScoringWeightDto
        {
            Id = w.Id,
            CriterionKey = w.CriterionKey,
            CriterionName = w.CriterionName,
            CriterionNameAr = w.CriterionNameAr,
            Weight = w.Weight,
            ModifiedBy = w.ModifiedBy,
            ModifiedAt = w.ModifiedAt
        }).ToList();
    }
}
