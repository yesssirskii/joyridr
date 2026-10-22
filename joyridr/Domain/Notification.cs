namespace Joyridr.Domain;

/// <summary>In-app notification inbox. Every notification is also sent as a push to the user's devices.</summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
    public Guid? RideId { get; set; }
    public Guid? CarId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
