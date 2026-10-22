using Joyridr.Data;
using Joyridr.Domain;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Features.Notifications;

/// <summary>
/// Call <see cref="Add"/> while making changes (the notification is saved in the same transaction),
/// then <see cref="SendPushesAsync"/> after SaveChanges succeeds.
/// </summary>
public class NotificationService(AppDbContext db, IPushSender pushSender, ILogger<NotificationService> logger)
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
}
