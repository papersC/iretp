namespace IRETP.Application.DTOs;

public class PlaybookDto
{
    public Guid ThresholdId { get; set; }
    public string IndicatorKey { get; set; } = default!;
    public string IndicatorName { get; set; } = default!;
    public List<string> Steps { get; set; } = [];
}

public class PlaybookProgressEntry
{
    public int StepIndex { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public string? Notes { get; set; }
}
