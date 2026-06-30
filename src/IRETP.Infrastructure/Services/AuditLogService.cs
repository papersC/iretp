using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;

namespace IRETP.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IRepository<AuditLog> _auditRepo;
    private readonly IUnitOfWork _unitOfWork;

    public AuditLogService(IRepository<AuditLog> auditRepo, IUnitOfWork unitOfWork)
    {
        _auditRepo = auditRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task LogAsync(string entityType, string entityId, string action, string? userId,
        string? userName = null, string? oldValues = null, string? newValues = null,
        string? ipAddress = null, CancellationToken ct = default)
    {
        var log = new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = userId,
            UserName = userName,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress
        };

        await _auditRepo.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
