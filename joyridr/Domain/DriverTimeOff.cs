namespace Joyridr.Domain;

/// <summary>A one-off block (holiday, car in service...) that overrides the weekly schedule.</summary>
public class DriverTimeOff
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DriverId { get; set; }
    public JoyDriverProfile Driver { get; set; } = null!;

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string? Reason { get; set; }
}
