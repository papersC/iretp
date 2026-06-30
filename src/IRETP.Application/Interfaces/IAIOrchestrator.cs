namespace IRETP.Application.Interfaces;

public interface IAIOrchestrator
{
    /// <summary>
    /// Answer a natural-language query. <paramref name="userId"/> is optional —
    /// when supplied and the user has opted into AI memory (RFP AI-006),
    /// cross-session preferences are injected into the RAG context and the
    /// interaction extends the user's memory footprint.
    /// </summary>
    Task<AIResponse> ProcessQueryAsync(string query, string language, string? sessionId, string? userId = null);
}

public class AIResponse
{
    public string Answer { get; set; } = default!;
    public string? ChartConfigJson { get; set; }
    public string? DataJson { get; set; }
    public string SourceCitation { get; set; } = default!;
    public string ModelUsed { get; set; } = default!;
}
