using Joyridr.Models.Enums;

namespace Joyridr.Models;

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Always stored lower-case.</summary>
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public JoyRiderProfile? RiderProfile { get; set; }
    public JoyDriverProfile? DriverProfile { get; set; }
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
