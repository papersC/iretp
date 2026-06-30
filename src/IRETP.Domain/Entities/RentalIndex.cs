using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class RentalIndex : BaseEntity
{
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = default!;
    public PropertyType UnitType { get; set; }
    public bool IsShortTerm { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal AverageAnnualRent { get; set; }
    public decimal GrossRentalYield { get; set; }
    public int SampleSize { get; set; }
}
