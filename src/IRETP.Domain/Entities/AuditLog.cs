using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string Action { get; set; } = default!; // Create, Update, Delete
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
}
