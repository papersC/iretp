using System.Security.Claims;
using IRETP.Application.Features.AIAgent.Commands;
using IRETP.Application.Interfaces;
using IRETP.Infrastructure.Services.Rag;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/ai")]
public class AIAgentController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IVectorStore _vectorStore;
    private readonly IComplianceAsk _complianceAsk;

    public AIAgentController(IMediator mediator, IVectorStore vectorStore, IComplianceAsk complianceAsk)
    {
        _mediator = mediator;
        _vectorStore = vectorStore;
        _complianceAsk = complianceAsk;
    }

    /// <summary>
    /// Send a natural-language query to the AI agent.
    /// </summary>
    [HttpPost("query")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Query([FromBody] AIQueryRequest request, CancellationToken ct)
    {
        var command = new ProcessAIQueryCommand
        {
            Query = request.Query,
            Language = request.Language ?? "en",
            SessionId = request.SessionId,
            UserId = User?.FindFirstValue(ClaimTypes.NameIdentifier)
        };

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Meta-Q&amp;A about the implementation itself: "how / why does the build
    /// address requirement X". Grounded on the Compliance Matrix, not the DLD
    /// data store — used by the evaluator-facing compliance search page.
    /// </summary>
    [HttpPost("compliance-ask")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ComplianceAsk([FromBody] ComplianceAskRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { message = "Provide a question" });

        var result = await _complianceAsk.AskAsync(request.Question.Trim(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Diagnostic: shows what the RAG vector store retrieves for a query — the
    /// records (with cosine scores) that ground the AI agent's answer.
    /// </summary>
    [HttpGet("retrieve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Retrieve([FromQuery] string q, [FromQuery] int topK = 8, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { message = "Provide a query via ?q=" });
        await _vectorStore.EnsureIndexedAsync(ct);
        var hits = _vectorStore.Search(q, topK);
        return Ok(new
        {
            query = q,
            documentsIndexed = _vectorStore.DocumentCount,
            lastIndexedUtc = _vectorStore.LastIndexedUtc,
            returned = hits.Count,
            hits = hits.Select(h => new
            {
                h.Doc.EntityType,
                h.Doc.EntityId,
                score = Math.Round(h.Score, 4),
                content = h.Doc.Content
            })
        });
    }
}

public class AIQueryRequest
{
    public string Query { get; set; } = default!;
    public string? Language { get; set; }
    public string? SessionId { get; set; }
}

public class ComplianceAskRequest
{
    public string Question { get; set; } = default!;
}
