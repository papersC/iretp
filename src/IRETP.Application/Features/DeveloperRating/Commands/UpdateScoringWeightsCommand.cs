using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Commands;

public class UpdateScoringWeightsCommand : IRequest<bool>
{
    public List<ScoringWeightUpdate> Weights { get; set; } = [];
    public string ModifiedBy { get; set; } = default!;
}

public class ScoringWeightUpdate
{
    public string CriterionKey { get; set; } = default!;
    public decimal Weight { get; set; }
}
