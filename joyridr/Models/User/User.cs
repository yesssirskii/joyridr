using Joyridr.Models.Enums;

namespace Joyridr.Models;

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Always stored lower-case.</summary>
    public string Email { get; set; } = null!;
    /// <summary>"ivan.kovac", shown as @ivan.kovac. Always stored lower-case; see <see cref="Common.UserNames"/>.</summary>
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Set when the user deleted their account. Their personal data is wiped at the same moment (see
    /// <see cref="Services.AccountService"/>); the row stays so past rides and payments still add up.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Set when an admin bans the user: they're logged out everywhere and can't log back in.</summary>
    public DateTimeOffset? BannedAt { get; set; }
    public string? BanReason { get; set; }

    public bool IsActive => DeletedAt is null && BannedAt is null;

    public JoyRiderProfile? RiderProfile { get; set; }
    public JoyDriverProfile? DriverProfile { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
