namespace Joyridr.Domain;

/// <summary>Rider-specific data. Shares its primary key with <see cref="User"/>.</summary>
public class JoyRiderProfile
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public int CompletedRides { get; set; }
}
