using IRETP.Domain.Enums;
using MediatR;

namespace IRETP.Application.Features.Map.Queries;

public class GetProjectsMapQuery : IRequest<List<ProjectMapPinDto>>
{
    public Guid? ZoneId { get; set; }
    public ProjectStatus? Status { get; set; }
    public Guid? DeveloperId { get; set; }
}

public class ProjectMapPinDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string DeveloperName { get; set; } = default!;
    public string ZoneName { get; set; } = default!;
    public ProjectStatus Status { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int TotalUnits { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
