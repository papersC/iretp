using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class MarkNotificationReadCommandHandler
    : IRequestHandler<MarkNotificationReadCommand, bool>
{
    private readonly IRepository<Notification> _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationReadCommandHandler(
        IRepository<Notification> notificationRepo,
        IUnitOfWork unitOfWork)
    {
        _notificationRepo = notificationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(
        MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notificationRepo.GetByIdAsync(request.Id, cancellationToken);

        if (notification is null || notification.UserId != request.UserId)
            return false;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;

        _notificationRepo.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
