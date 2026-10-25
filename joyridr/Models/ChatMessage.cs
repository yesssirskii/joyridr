namespace Joyridr.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public Guid SenderId { get; set; }
    public User Sender { get; set; } = null!;

    public string Body { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}
