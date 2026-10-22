using Joyridr.Common;
using System.ComponentModel.DataAnnotations;
using Joyridr.Domain;
using Joyridr.Features.Cars;
using Joyridr.Features.Drivers;

namespace Joyridr.Features.Rides;

public record CreateRideRequest(
    Guid CarId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    MeetingPointType MeetingPointType,
    [property: Range(-90, 90)] double? MeetingLatitude,
    [property: Range(-180, 180)] double? MeetingLongitude,
    [property: MaxLength(300)] string? MeetingAddress,
    [property: MaxLength(1000)] string? Note);

public record UpdateMeetingPointRequest(
    [property: Range(-90, 90)] double Latitude,
    [property: Range(-180, 180)] double Longitude,
    [property: MaxLength(300)] string? Address);

public record ReasonRequest([property: MaxLength(500)] string? Reason);

public record CreateReviewRequest([property: Range(1, 5)] int Rating, [property: MaxLength(1000)] string? Comment);

public enum RideListView
{
    /// <summary>Awaiting payment, requested, confirmed or in progress. Soonest first.</summary>
    Upcoming,
    /// <summary>Completed, declined, cancelled or expired. Most recent first.</summary>
    Past,
    /// <summary>Driver only: requests waiting for a confirm/decline. Most urgent first.</summary>
    Requests
}

public record MeetingPointDto(MeetingPointType Type, double? Latitude, double? Longitude, string? Address);

/// <summary>The other person on the ride. Phone number is only shared once the ride is confirmed.</summary>
public record RideParticipantDto(Guid Id, string DisplayName, string? AvatarUrl, double RatingAverage, int RatingCount, string? PhoneNumber);

/// <summary><see cref="ClientSecret"/> is only set right after booking, when a real payment provider needs the app to finish paying.</summary>
public record PaymentDto(PaymentStatus Status, decimal Amount, decimal RefundedAmount, string Currency, string? ClientSecret);

public record ReviewDto(Guid Id, int Rating, string? Comment, string AuthorName, string? AuthorAvatarUrl, DateTimeOffset CreatedAt)
{
    public static ReviewDto From(Review r) =>
        new(r.Id, r.Rating, r.Comment, UserNames.Display(r.Author), r.Author.AvatarUrl, r.CreatedAt);
}

public record RideDto(
    Guid Id,
    RideStatus Status,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int DurationMinutes,
    CarSummaryDto Car,
    RideParticipantDto Rider,
    RideParticipantDto Driver,
    MeetingPointDto MeetingPoint,
    string? RiderNote,
    decimal Price,
    string Currency,
    DateTimeOffset? RespondBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? DeclinedAt,
    string? DeclineReason,
    DateTimeOffset? CancelledAt,
    bool? CancelledByMe,
    string? CancellationReason,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    PaymentDto? Payment,
    bool CanReview)
{
    /// <summary>Needs Rider.RiderProfile, Driver.DriverProfile, Car.Photos, Payment and Reviews loaded.</summary>
    public static RideDto From(Ride r, Guid viewerId, string? clientSecret = null)
    {
        var sharePhone = r.Status is RideStatus.Confirmed or RideStatus.InProgress;
        return new RideDto(
            r.Id, r.Status, r.StartsAt, r.EndsAt, r.DurationMinutes,
            CarSummaryDto.From(r.Car),
            new RideParticipantDto(r.Rider.Id, UserNames.Display(r.Rider), r.Rider.AvatarUrl,
                r.Rider.RiderProfile?.RatingAverage ?? 0, r.Rider.RiderProfile?.RatingCount ?? 0,
                sharePhone ? r.Rider.PhoneNumber : null),
            new RideParticipantDto(r.Driver.Id, UserNames.Display(r.Driver), r.Driver.AvatarUrl,
                r.Driver.DriverProfile?.RatingAverage ?? 0, r.Driver.DriverProfile?.RatingCount ?? 0,
                sharePhone ? r.Driver.PhoneNumber : null),
            new MeetingPointDto(r.MeetingPointType, r.MeetingLatitude, r.MeetingLongitude, r.MeetingAddress),
            r.RiderNote, r.Price, r.Currency, r.RespondBy, r.CreatedAt, r.ConfirmedAt, r.DeclinedAt, r.DeclineReason,
            r.CancelledAt, r.CancelledById is null ? null : r.CancelledById == viewerId, r.CancellationReason,
            r.StartedAt, r.CompletedAt,
            r.Payment is null ? null : new PaymentDto(r.Payment.Status, r.Payment.Amount, r.Payment.RefundedAmount, r.Payment.Currency, clientSecret),
            CanReview: r.Status == RideStatus.Completed && r.Reviews.All(rev => rev.AuthorId != viewerId));
    }
}
