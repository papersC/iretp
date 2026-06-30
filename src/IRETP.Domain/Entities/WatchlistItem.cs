using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class WatchlistItem : BaseEntity
{
    public string UserId { get; set; } = default!;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public Guid? DeveloperId { get; set; }
    public Developer? Developer { get; set; }
}
