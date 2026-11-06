namespace Joyridr.Models;

/// <summary>
/// A 6-digit code emailed for "forgot password". Only a keyed hash is stored. It works once, for 15 minutes,
/// and stops working after 5 wrong tries. Asking for a new code cancels the old one.
/// </summary>
public class PasswordResetCode
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string CodeHash { get; set; } = null!;
    public int Attempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>Set when the code was used, or cancelled by a newer one.</summary>
    public DateTimeOffset? UsedAt { get; set; }
}
