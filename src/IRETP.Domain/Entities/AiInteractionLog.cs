using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

/// <summary>
/// Query-by-query audit record for the AI Agent (RFP Section 5 · FR005
/// DLD Service Navigation traceability and RFP 15.3 Incident Reporting).
/// Each row captures what was asked, what was answered, which model served
/// the request, and end-to-end latency so the admin AI Models page can
/// reconstruct incidents and operations can answer the "who asked what when"
/// question during a VAPT or DESC audit.
/// </summary>
public class AiInteractionLog : BaseEntity
{
    public string? SessionId { get; set; }
    public string? UserId { get; set; }

    public string Language { get; set; } = "en";
    public string Query { get; set; } = default!;

    /// <summary>Classified topic — used for DLD Service Navigation reporting (FR005).</summary>
    public string Topic { get; set; } = "general";

    public string Answer { get; set; } = default!;
    public string? SourceCitation { get; set; }
    public string? ModelUsed { get; set; }

    /// <summary>Whether the Agent refused the query under the no-investment-advice guardrail.</summary>
    public bool WasRefusal { get; set; }

    public int LatencyMs { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
