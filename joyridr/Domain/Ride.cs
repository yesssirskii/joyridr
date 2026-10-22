namespace Joyridr.Domain;

public class Ride
{
    /// <summary>Statuses that occupy the driver's time. Kept in sync with the overlap constraint in the migrations.</summary>
    public static readonly RideStatus[] BlockingStatuses =
        [RideStatus.AwaitingPayment, RideStatus.Requested, RideStatus.Confirmed, RideStatus.InProgress];

    public static readonly RideStatus[] FinishedStatuses =
        [RideStatus.Completed, RideStatus.Declined, RideStatus.Cancelled, RideStatus.Expired];

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid RiderId { get; set; }
    public User Rider { get; set; } = null!;
    public Guid DriverId { get; set; }
    public User Driver { get; set; } = null!;
    public Guid CarId { get; set; }
    public Car Car { get; set; } = null!;

    public RideStatus Status { get; set; }

    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int DurationMinutes { get; set; }

    public MeetingPointType MeetingPointType { get; set; }
    public double? MeetingLatitude { get; set; }
    public double? MeetingLongitude { get; set; }
    public string? MeetingAddress { get; set; }

    public string? RiderNote { get; set; }

    /// <summary>Price snapshot at booking time, so later car price changes don't affect it.</summary>
    public decimal Price { get; set; }
    public string Currency { get; set; } = "EUR";

    /// <summary>Driver must confirm or decline before this, otherwise the ride expires and the rider is refunded.</summary>
    public DateTimeOffset? RespondBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? DeclinedAt { get; set; }
    public string? DeclineReason { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledById { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Maps to Postgres' xmin; stops e.g. a confirm and a cancel from both succeeding at the same time.</summary>
    public uint Version { get; set; }

    public Payment? Payment { get; set; }
    public List<Review> Reviews { get; set; } = [];
}
