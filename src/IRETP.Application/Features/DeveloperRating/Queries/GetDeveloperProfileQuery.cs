using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.DeveloperRating.Queries;

public class GetDeveloperProfileQuery : IRequest<DeveloperProfileDto?>
{
    public Guid DeveloperId { get; set; }
}
