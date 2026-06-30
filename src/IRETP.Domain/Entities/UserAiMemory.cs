using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

/// <summary>
/// RFP AI-006 — cross-session memory for authenticated users who have
/// opted in via <c>ConsentAiMemory</c>. Stores per-user frequency of zone
/// mentions and topic categories so the AI Agent can personalise future
/// responses. Deleted on consent revocation (PDPL §19.2).
/// </summary>
public class UserAiMemory : BaseEntity
{
    public string UserId { get; set; } = default!;

    /// <summary>Either <c>"zone"</c> (zone name) or <c>"topic"</c> (AIOrchestrator topic bucket).</summary>
    public string Kind { get; set; } = default!;

    /// <summary>Zone name or topic key — e.g., <c>"Dubai Marina"</c> or <c>"rental"</c>.</summary>
    public string Key { get; set; } = default!;

    public int Frequency { get; set; }

    public DateTime LastUsedAt { get; set; } = DateTime.UtcNow;
}
