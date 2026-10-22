namespace Joyridr.Features.Rides;

/// <summary>Business rules for booking, bound from the "Rides" config section.</summary>
public class RideOptions
{
    public const string SectionName = "Rides";

    /// <summary>How long before the start a ride can still be booked.</summary>
    public int MinLeadTimeMinutes { get; set; } = 120;
    public int MaxAdvanceDays { get; set; } = 60;

    public int MinDurationMinutes { get; set; } = 30;
    public int MaxDurationMinutes { get; set; } = 240;

    /// <summary>Rides start and last in steps of this many minutes (also the slot grid).</summary>
    public int StepMinutes { get; set; } = 30;

    /// <summary>The driver has this long to confirm (but never later than 1h before the start).</summary>
    public int DriverResponseHours { get; set; } = 24;

    /// <summary>How long a slot is held while the rider completes payment.</summary>
    public int PaymentWindowMinutes { get; set; } = 15;

    /// <summary>A rider who cancels a confirmed ride at least this many hours before the start gets a full refund.</summary>
    public int FreeCancellationHours { get; set; } = 24;
}
