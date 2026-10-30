namespace Joyridr.Models;

/// <summary>
/// A chat between one rider and one driver. There is at most one per pair, and it isn't tied to a
/// ride, so a rider can ask questions before booking.
/// </summary>
public class Conversation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RiderId { get; set; }
    public User Rider { get; set; } = null!;
    public Guid DriverId { get; set; }
    public User Driver { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Used to sort the inbox. Null until the first message.</summary>
    public DateTimeOffset? LastMessageAt { get; set; }

    /// <summary>Messages from the other side newer than this are unread.</summary>
    public DateTimeOffset? RiderLastReadAt { get; set; }
    public DateTimeOffset? DriverLastReadAt { get; set; }

    public List<ChatMessage> Messages { get; set; } = [];
}
