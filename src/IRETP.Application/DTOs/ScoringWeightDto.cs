namespace IRETP.Application.DTOs;

public class ScoringWeightDto
{
    public Guid Id { get; set; }
    public string CriterionKey { get; set; } = default!;
    public string CriterionName { get; set; } = default!;
    public string CriterionNameAr { get; set; } = default!;
    public decimal Weight { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
