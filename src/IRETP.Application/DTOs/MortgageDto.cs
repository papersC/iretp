namespace IRETP.Application.DTOs;

/// <summary>
/// Surface of the Mortgage &amp; Debt Market Transparency page (RFP Section 20
/// — flagged by GRETI 2024 as a key UAE improvement area). Every metric is
/// derived from DLD-registered transactions filtered on mortgage status so
/// the figures are traceable to a single authoritative source.
/// </summary>
public class MortgageDashboardDto
{
    public int TotalMortgageRecords { get; set; }
    public decimal TotalRegisteredValueAed { get; set; }
    public decimal MortgageValueShareOfAllTransactionsPct { get; set; }
    public decimal AverageMortgageValueAed { get; set; }
    public decimal MomValueChangePct { get; set; }

    public List<MortgageZoneItem> ByZone { get; set; } = [];
    public List<MortgageMonthPoint> Trend { get; set; } = [];
    public List<MortgagePropertyTypeItem> ByPropertyType { get; set; } = [];
}

public class MortgageZoneItem
{
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = default!;
    public int MortgageCount { get; set; }
    public decimal MortgageValue { get; set; }
    public decimal MortgageSharePct { get; set; }     // of all transactions in the zone by value
    public decimal AverageLtvProxyPct { get; set; }   // mortgage value as % of sale value in the same zone
}

public class MortgageMonthPoint
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Label { get; set; } = default!;     // yyyy-MM
    public int Count { get; set; }
    public decimal Value { get; set; }
}

public class MortgagePropertyTypeItem
{
    public string PropertyType { get; set; } = default!;
    public int Count { get; set; }
    public decimal Value { get; set; }
}
