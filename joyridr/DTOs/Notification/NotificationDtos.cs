using System.ComponentModel.DataAnnotations;
using Joyridr.Models;
using Joyridr.Models.Enums;

namespace Joyridr.DTOs;

public record NotificationDto(Guid Id, NotificationType Type, string Title, string Body, Guid? RideId, Guid? CarId, DateTimeOffset CreatedAt, bool IsRead)
{
    public static NotificationDto From(Notification n) => new(n.Id, n.Type, n.Title, n.Body, n.RideId, n.CarId, n.CreatedAt, n.ReadAt is not null);
}

public record RegisterDeviceRequest([Required, MaxLength(256)] string Token);

public record UnreadCountDto(int Count);
