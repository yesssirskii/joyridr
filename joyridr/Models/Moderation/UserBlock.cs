namespace Joyridr.Models;

/// <summary>
/// <see cref="Blocker"/> blocked <see cref="Blocked"/>. Works both ways: neither can message the other,
/// they can't book each other, and the blocked driver's cars disappear from the blocker's search.
/// </summary>
public class UserBlock
{
    public Guid BlockerId { get; set; }
    public User Blocker { get; set; } = null!;
    public Guid BlockedId { get; set; }
    public User Blocked { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
