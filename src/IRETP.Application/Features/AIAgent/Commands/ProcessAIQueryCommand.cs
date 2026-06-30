using IRETP.Application.Interfaces;
using MediatR;

namespace IRETP.Application.Features.AIAgent.Commands;

public class ProcessAIQueryCommand : IRequest<AIResponse>
{
    public string Query { get; set; } = default!;
    public string Language { get; set; } = "en";
    public string? SessionId { get; set; }

    /// <summary>Set from the caller's ClaimsPrincipal (NameIdentifier claim).</summary>
    public string? UserId { get; set; }
}
