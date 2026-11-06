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

public enum ReportReason
{
    Spam,
    Harassment,
    Inappropriate,
    /// <summary>Scams, fake listings, asking to pay outside the app.</summary>
    Scam,
    Other
}

/// <summary>What was reported. Kept even after the message or review is removed.</summary>
public enum ReportKind
{
    User,
    Message,
    Review
}

public enum ReportStatus
{
    Open,
    /// <summary>An admin looked at it and decided nothing needed doing.</summary>
    Dismissed,
    /// <summary>The content was removed or the user banned (or the account was deleted).</summary>
    Resolved
}

public enum NotificationType
{
    RideRequested,
    /// <summary>Sent to the driver shortly before an unanswered request expires.</summary>
    RideRequestReminder,
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
