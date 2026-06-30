using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

public class UpdatePlaybookCommand : IRequest<bool>
{
    public Guid ThresholdId { get; set; }
    public List<string> Steps { get; set; } = [];
    public string? ModifiedBy { get; set; }
}
