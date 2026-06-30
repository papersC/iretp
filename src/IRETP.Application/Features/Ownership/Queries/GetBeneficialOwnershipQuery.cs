using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Ownership.Queries;

public class GetBeneficialOwnershipQuery : IRequest<List<BeneficialOwnershipDto>>
{
    public Guid DeveloperId { get; set; }
}
