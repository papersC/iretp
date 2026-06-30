using MediatR;

namespace IRETP.Application.Features.Analytics.Commands;

public class SaveAnalyticsViewCommand : IRequest<Guid>
{
    public string? UserId { get; set; }
    public string Name { get; set; } = default!;
    public string ConfigurationJson { get; set; } = default!;
    public bool IsPublic { get; set; }
    public int DisplayOrder { get; set; }
}
