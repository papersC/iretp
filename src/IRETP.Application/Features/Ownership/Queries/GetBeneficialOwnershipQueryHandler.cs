using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Ownership.Queries;

public class GetBeneficialOwnershipQueryHandler
    : IRequestHandler<GetBeneficialOwnershipQuery, List<BeneficialOwnershipDto>>
{
    private readonly IRepository<BeneficialOwner> _repo;

    public GetBeneficialOwnershipQueryHandler(IRepository<BeneficialOwner> repo)
    {
        _repo = repo;
    }

    public async Task<List<BeneficialOwnershipDto>> Handle(
        GetBeneficialOwnershipQuery request, CancellationToken cancellationToken)
    {
        return await _repo.Query()
            .Where(o => o.DeveloperId == request.DeveloperId)
            .OrderByDescending(o => o.OwnershipPct)
            .Select(o => new BeneficialOwnershipDto
            {
                Id = o.Id,
                DeveloperId = o.DeveloperId,
                OwnerName = o.OwnerName,
                OwnerNameAr = o.OwnerNameAr,
                OwnerType = o.OwnerType,
                CountryOfIncorporation = o.CountryOfIncorporation,
                OwnershipPct = o.OwnershipPct,
                DisclosedAt = o.DisclosedAt,
                DisclosureSource = o.DisclosureSource
            })
            .ToListAsync(cancellationToken);
    }
}
