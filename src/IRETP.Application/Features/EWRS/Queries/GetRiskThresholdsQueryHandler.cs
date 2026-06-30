using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.EWRS.Queries;

public class GetRiskThresholdsQueryHandler
    : IRequestHandler<GetRiskThresholdsQuery, IReadOnlyList<RiskThreshold>>
{
    private readonly IRepository<RiskThreshold> _thresholdRepo;

    public GetRiskThresholdsQueryHandler(IRepository<RiskThreshold> thresholdRepo)
    {
        _thresholdRepo = thresholdRepo;
    }

    public async Task<IReadOnlyList<RiskThreshold>> Handle(
        GetRiskThresholdsQuery request, CancellationToken cancellationToken)
    {
        return await _thresholdRepo.GetAllAsync(cancellationToken);
    }
}
