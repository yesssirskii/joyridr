namespace Joyridr.Models.Enums;

public enum UserRole
{
    JoyRider,
    JoyDriver,
    Admin
}

public enum CarCategory
{
    SportsCar,
    Supercar,
    Hypercar
}

/// <summary>
/// Every car is reviewed by an admin before it's listed, which is how we keep the
/// platform sports/supercar only.
/// </summary>
public enum CarStatus
{
    PendingReview,
    Approved,
    Rejected
}

public enum Transmission
{
    Manual,
    Automatic
}

public enum RideStatus
{
    /// <summary>Slot is held while the rider completes payment.</summary>
    AwaitingPayment,
    /// <summary>Paid (authorized), waiting for the driver to confirm.</summary>
    Requested,
    Confirmed,
    InProgress,
    Completed,
    Declined,
    Cancelled,
    /// <summary>Driver didn't respond in time, or payment was never completed.</summary>
    Expired
}

public enum MeetingPointType
{
    /// <summary>Driver picks the rider up at the rider's location.</summary>
    DriverComesToRider,
    /// <summary>Rider goes to the driver's base location.</summary>
    RiderComesToDriver,
    /// <summary>Rider and driver agree on a location (can be updated until the ride starts).</summary>
    AgreedLocation
}

public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Voided,
    Refunded,
    PartiallyRefunded,
    Failed
}

public enum NotificationType
{
    RideRequested,
    RideConfirmed,
    RideDeclined,
    RideCancelled,
    RideExpired,
    RideStarted,
    RideCompleted,
    MeetingPointUpdated,
    CarApproved,
    CarRejected,
    NewReview
}
