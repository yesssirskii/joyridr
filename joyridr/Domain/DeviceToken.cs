namespace Joyridr.Domain;

/// <summary>APNs device token registered by the iOS app, used for push notifications.</summary>
public class DeviceToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Token { get; set; } = null!;
    public string Platform { get; set; } = "ios";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
