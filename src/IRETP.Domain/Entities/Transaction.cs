using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class Transaction : BaseEntity
{
    public DateTime TransactionDate { get; set; }
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = default!;
    public string? Community { get; set; }
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public string? ProjectName { get; set; }
    public PropertyType PropertyType { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal AreaSqft { get; set; }
    public decimal AreaSqm { get; set; }
    public decimal TransactionValue { get; set; }
    public decimal PricePerSqft { get; set; }
    public FinancingMethod FinancingMethod { get; set; }
    public bool IsOffPlan { get; set; }
}
