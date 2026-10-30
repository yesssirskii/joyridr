namespace Joyridr.Models;

/// <summary>
/// Refresh tokens are rotated on every use. All tokens descending from one login share a
/// <see cref="FamilyId"/>, so if a revoked token is ever presented again (a sign it was stolen)
/// the whole family is revoked.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>SHA-256 of the token. The raw token is only ever sent to the client.</summary>
    public string TokenHash { get; set; } = null!;
    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
