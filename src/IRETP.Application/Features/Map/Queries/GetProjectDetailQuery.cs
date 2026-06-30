using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Map.Queries;

public class GetProjectDetailQuery : IRequest<ProjectDetailDto?>
{
    public Guid ProjectId { get; set; }
}
