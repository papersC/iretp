using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class PriceIndex : BaseEntity
{
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = default!;
    public PropertyType PropertyType { get; set; }
    public bool IsOffPlan { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public int? Month { get; set; }
    public decimal AveragePricePerSqft { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalValue { get; set; }
    public decimal? QuarterlyChange { get; set; }
    public decimal? AnnualChange { get; set; }
}
