namespace IRETP.Application.Interfaces;

/// <summary>
/// Answers free-form "how does X work / why was X done this way" questions
/// about the IRETP build for RFP DLD-IRETP-2026-001. Grounded on the
/// technical documentation pack (docs/*.md — compliance matrix, architecture,
/// integration map, API reference, …) rather than the DLD data RAG store —
/// this is meta-Q&amp;A about the implementation itself, aimed at evaluators
/// and demo prep, not at market-data queries.
/// </summary>
public interface IComplianceAsk
{
    Task<ComplianceAskResult> AskAsync(string question, CancellationToken ct = default);
}

public sealed class ComplianceAskResult
{
    /// <summary>Model answer, or a fallback explanation when no AI tier is configured.</summary>
    public string Answer { get; init; } = default!;

    /// <summary>Model that produced the answer, null when the fallback path ran.</summary>
    public string? ModelUsed { get; init; }

    /// <summary>Compliance-matrix rows/sections the answer was grounded on.</summary>
    public IReadOnlyList<string> Sources { get; init; } = [];
}
