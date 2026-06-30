namespace IRETP.Application.DTOs;

public class AiInteractionLogDto
{
    public Guid Id { get; set; }
    public string? SessionId { get; set; }
    public string? UserId { get; set; }
    public string Language { get; set; } = "en";
    public string Query { get; set; } = default!;
    public string Topic { get; set; } = "general";
    public string Answer { get; set; } = default!;
    public string? SourceCitation { get; set; }
    public string? ModelUsed { get; set; }
    public bool WasRefusal { get; set; }
    public int LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}
