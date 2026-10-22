using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

/// <summary>
/// Sending: call <see cref="Add"/> while making changes (the notification is saved in the same
/// transaction), then <see cref="SendPushesAsync"/> after SaveChanges succeeds.
/// Also serves the in-app inbox and APNs device registration.
/// </summary>
public class NotificationService(AppDbContext db, IPushSender pushSender, TimeProvider clock, ILogger<NotificationService> logger)
{
    private readonly List<Notification> _pending = [];

    public void Add(Guid userId, NotificationType type, string title, string body, Guid? rideId = null, Guid? carId = null)
    {
        var notification = new Notification { UserId = userId, Type = type, Title = title, Body = body, RideId = rideId, CarId = carId };
        db.Notifications.Add(notification);
        _pending.Add(notification);
    }

    public async Task SendPushesAsync(CancellationToken ct)
    {
        var toSend = _pending.ToList();
        _pending.Clear();

        foreach (var notification in toSend)
        {
            try
            {
                var tokens = await db.DeviceTokens.Where(d => d.UserId == notification.UserId).Select(d => d.Token).ToListAsync(ct);
                if (tokens.Count == 0)
                    continue;

                var data = new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(),
                    ["type"] = notification.Type.ToString(),
                };
                if (notification.RideId is not null) data["rideId"] = notification.RideId.Value.ToString();
                if (notification.CarId is not null) data["carId"] = notification.CarId.Value.ToString();

                await pushSender.SendAsync(tokens, notification.Title, notification.Body, data, ct);
            }
            catch (Exception ex)
            {
                // A failed push should never fail the request; the notification is still in the inbox.
                logger.LogWarning(ex, "Failed to send push for notification {NotificationId}", notification.Id);
            }
        }
    }

    // ---- inbox ----

    public async Task<PagedResult<NotificationDto>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<NotificationDto>(items.Select(NotificationDto.From).ToList(), page, pageSize, total);
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken ct) =>
        new(await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct));

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.Notifications.Where(n => n.Id == notificationId && n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    // ---- devices ----

    public async Task RegisterDeviceAsync(Guid userId, string token, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // A device token belongs to whoever logged in on that phone most recently.
        var device = await db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == token, ct);
        if (device is null)
            db.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = token, LastSeenAt = now });
        else
        {
            device.UserId = userId;
            device.LastSeenAt = now;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task UnregisterDeviceAsync(Guid userId, string token, CancellationToken ct) =>
        await db.DeviceTokens.Where(d => d.Token == token && d.UserId == userId).ExecuteDeleteAsync(ct);
}
