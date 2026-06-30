using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Commands;

public class MarkAllNotificationsReadCommandHandler
    : IRequestHandler<MarkAllNotificationsReadCommand, int>
{
    private readonly IRepository<Notification> _notificationRepo;
    private readonly IUnitOfWork _unitOfWork;

    public MarkAllNotificationsReadCommandHandler(
        IRepository<Notification> notificationRepo,
        IUnitOfWork unitOfWork)
    {
        _notificationRepo = notificationRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(
        MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        var unread = await _notificationRepo.FindAsync(
            n => n.UserId == request.UserId && !n.IsRead, cancellationToken);

        var now = DateTime.UtcNow;
        var count = 0;

        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
            _notificationRepo.Update(notification);
            count++;
        }

        if (count > 0)
            await _unitOfWork.SaveChangesAsync(cancellationToken);

        return count;
    }
}
