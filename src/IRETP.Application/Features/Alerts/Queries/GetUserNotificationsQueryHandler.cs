using IRETP.Application.Common;
using IRETP.Application.DTOs;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using MediatR;

namespace IRETP.Application.Features.Alerts.Queries;

public class GetUserNotificationsQueryHandler
    : IRequestHandler<GetUserNotificationsQuery, PagedResult<NotificationDto>>
{
    private readonly IRepository<Notification> _notificationRepo;

    public GetUserNotificationsQueryHandler(IRepository<Notification> notificationRepo)
    {
        _notificationRepo = notificationRepo;
    }

    public async Task<PagedResult<NotificationDto>> Handle(
        GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        var query = _notificationRepo.Query()
            .Where(n => n.UserId == request.UserId);

        if (request.IsRead.HasValue)
            query = query.Where(n => n.IsRead == request.IsRead.Value);

        query = query.OrderByDescending(n => n.CreatedAt);

        var totalCount = query.Count();

        var items = query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                TitleAr = n.TitleAr,
                Message = n.Message,
                MessageAr = n.MessageAr,
                Link = n.Link,
                Channel = n.Channel,
                Category = n.Category,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt
            })
            .ToList();

        return await Task.FromResult(
            new PagedResult<NotificationDto>(items, totalCount, request.Page, request.PageSize));
    }
}
