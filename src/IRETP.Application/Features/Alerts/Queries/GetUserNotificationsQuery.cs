using IRETP.Application.Common;
using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.Alerts.Queries;

public class GetUserNotificationsQuery : IRequest<PagedResult<NotificationDto>>
{
    public string? UserId { get; set; }
    public bool? IsRead { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
