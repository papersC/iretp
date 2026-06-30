using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class ApiKey : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public long RequestCount { get; set; }
    public int RateLimitPerMinute { get; set; } = 60;
}
