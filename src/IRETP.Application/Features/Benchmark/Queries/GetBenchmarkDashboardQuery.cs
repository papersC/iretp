using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Benchmark.Queries;

public class GetBenchmarkDashboardQuery : IRequest<BenchmarkDashboardDto>
{
}
