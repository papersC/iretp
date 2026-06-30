using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Application.Features.Map.Queries;

public class GetProjectsMapQueryHandler : IRequestHandler<GetProjectsMapQuery, List<ProjectMapPinDto>>
{
    private readonly IRepository<Project> _projectRepo;

    public GetProjectsMapQueryHandler(IRepository<Project> projectRepo)
    {
        _projectRepo = projectRepo;
    }

    public async Task<List<ProjectMapPinDto>> Handle(GetProjectsMapQuery request, CancellationToken cancellationToken)
    {
        var query = _projectRepo.Query()
            .Include(p => p.Developer)
            .Include(p => p.Zone)
            .AsQueryable();

        if (request.ZoneId.HasValue)
            query = query.Where(p => p.ZoneId == request.ZoneId.Value);

        if (request.Status.HasValue)
            query = query.Where(p => p.Status == request.Status.Value);

        if (request.DeveloperId.HasValue)
            query = query.Where(p => p.DeveloperId == request.DeveloperId.Value);

        return await query.Select(p => new ProjectMapPinDto
        {
            Id = p.Id,
            Name = p.Name,
            NameAr = p.NameAr,
            DeveloperName = p.Developer.Name,
            ZoneName = p.Zone.Name,
            Status = p.Status,
            CompletionPercentage = p.CompletionPercentage,
            TotalUnits = p.TotalUnits,
            Latitude = p.Latitude,
            Longitude = p.Longitude
        }).ToListAsync(cancellationToken);
    }
}
