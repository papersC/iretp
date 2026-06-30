namespace IRETP.Application.DTOs;

public class NameValidationDto
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = default!;
    public Guid EntityId { get; set; }
    public string NameEn { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string? OfficialNameAr { get; set; }
    public int Status { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NameValidationSummaryDto
{
    public int TotalEntities { get; set; }
    public int Pending { get; set; }
    public int Validated { get; set; }
    public int Rejected { get; set; }
    public int NeedsCorrection { get; set; }
    public decimal CompletionPct => TotalEntities == 0 ? 0m
        : Math.Round((decimal)Validated / TotalEntities * 100m, 1);
}
