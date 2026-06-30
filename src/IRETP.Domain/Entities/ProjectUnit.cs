using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class ProjectUnit : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = default!;
    public PropertyType PropertyType { get; set; }
    public int Count { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal AverageSizeSqft { get; set; }
}
