using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class Zone : BaseEntity
{
    public string Name { get; set; } = default!;
    public string NameAr { get; set; } = default!;
    public string? ParentArea { get; set; }
    public string? ParentAreaAr { get; set; }
    public string? GeoJson { get; set; }
    public double? CenterLat { get; set; }
    public double? CenterLng { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = [];
    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<PriceIndex> PriceIndices { get; set; } = [];
    public ICollection<RentalIndex> RentalIndices { get; set; } = [];
}
