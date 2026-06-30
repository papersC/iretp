namespace IRETP.Application.Interfaces;

/// <summary>
/// Writes append-only entries to the central <c>AuditLogs</c> table. Used by
/// admin command handlers to record RBAC-sensitive changes alongside the
/// per-row ModifiedBy/ModifiedAt fields, so DLD internal audit can query
/// activity by actor across entity types (RFP §10.2 + §9.1.2 + §8.3).
/// </summary>
public interface IAuditLogService
{
    Task LogAsync(
        string entityType,
        string entityId,
        string action,
        string? userId,
        string? userName = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        CancellationToken ct = default);
}
