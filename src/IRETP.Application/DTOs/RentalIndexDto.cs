using IRETP.Domain.Enums;

namespace IRETP.Application.DTOs;

public class RentalIndexDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public string ZoneNameAr { get; set; } = default!;
    public PropertyType UnitType { get; set; }
    public bool IsShortTerm { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public decimal AverageAnnualRent { get; set; }
    public decimal GrossRentalYield { get; set; }
    public int SampleSize { get; set; }
}

public class RentalIndexTrendDto
{
    public List<RentalIndexDto> DataPoints { get; set; } = [];
    public decimal CurrentAvgRent { get; set; }
    public decimal CurrentAvgYield { get; set; }
}

public class RentalYieldCalculationDto
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public string ZoneNameAr { get; set; } = default!;
    public PropertyType UnitType { get; set; }
    public decimal AverageAnnualRent { get; set; }
    public decimal AverageTransactionPrice { get; set; }
    public decimal GrossRentalYield { get; set; }
}
