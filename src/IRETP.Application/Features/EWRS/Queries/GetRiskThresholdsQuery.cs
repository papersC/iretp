using IRETP.Domain.Entities;
using MediatR;

namespace IRETP.Application.Features.EWRS.Queries;

public class GetRiskThresholdsQuery : IRequest<IReadOnlyList<RiskThreshold>>;
