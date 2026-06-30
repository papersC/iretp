using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class ScoringWeight : BaseEntity
{
    public string CriterionKey { get; set; } = default!;
    public string CriterionName { get; set; } = default!;
    public string CriterionNameAr { get; set; } = default!;
    public decimal Weight { get; set; }
    public string? ModifiedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
