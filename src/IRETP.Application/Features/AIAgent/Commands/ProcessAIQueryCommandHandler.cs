using IRETP.Application.Interfaces;
using MediatR;

namespace IRETP.Application.Features.AIAgent.Commands;

public class ProcessAIQueryCommandHandler : IRequestHandler<ProcessAIQueryCommand, AIResponse>
{
    private readonly IAIOrchestrator _aiOrchestrator;

    public ProcessAIQueryCommandHandler(IAIOrchestrator aiOrchestrator)
    {
        _aiOrchestrator = aiOrchestrator;
    }

    public async Task<AIResponse> Handle(
        ProcessAIQueryCommand request, CancellationToken cancellationToken)
    {
        return await _aiOrchestrator.ProcessQueryAsync(
            request.Query, request.Language, request.SessionId, request.UserId);
    }
}
