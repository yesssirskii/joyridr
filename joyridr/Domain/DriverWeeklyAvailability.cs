namespace Joyridr.Domain;

/// <summary>
/// A recurring weekly window when the driver is available, in the driver's local time
/// (<see cref="JoyDriverProfile.TimeZoneId"/>). A day can have several windows.
/// Windows can't cross midnight; split them into two instead.
/// </summary>
public class DriverWeeklyAvailability
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DriverId { get; set; }
    public JoyDriverProfile Driver { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}
