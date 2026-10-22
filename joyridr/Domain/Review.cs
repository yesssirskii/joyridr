namespace Joyridr.Domain;

/// <summary>After a completed ride, the rider can review the driver and the driver can review the rider.</summary>
public class Review
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RideId { get; set; }
    public Ride Ride { get; set; } = null!;

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;
    public Guid SubjectId { get; set; }
    public User Subject { get; set; } = null!;

    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
