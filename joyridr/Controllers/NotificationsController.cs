using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

[Route("api/notifications")]
[Authorize]
public class NotificationsController(NotificationService notifications) : ApiControllerBase
{
    /// <summary>The user's notification inbox, newest first.</summary>
    [HttpGet]
    public Task<PagedResult<NotificationDto>> List(CancellationToken ct, bool unreadOnly = false, int page = 1, int pageSize = 30) =>
        notifications.ListAsync(CurrentUserId, unreadOnly, page, pageSize, ct);

    /// <summary>For the badge on the notifications tab.</summary>
    [HttpGet("unread-count")]
    public Task<UnreadCountDto> UnreadCount(CancellationToken ct) => notifications.GetUnreadCountAsync(CurrentUserId, ct);

    /// <summary>Mark one notification as read.</summary>
    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken ct)
    {
        await notifications.MarkReadAsync(CurrentUserId, notificationId, ct);
        return NoContent();
    }

    /// <summary>Mark everything as read.</summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(CurrentUserId, ct);
        return NoContent();
    }
}
