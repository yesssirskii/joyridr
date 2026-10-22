using System.ComponentModel.DataAnnotations;
using Joyridr.Common;
using Joyridr.Domain;
using Joyridr.Features.Cars;
using Joyridr.Features.Rides;

namespace Joyridr.Features.Drivers;

/// <summary>The driver's own, full profile (includes their private base address).</summary>
public record DriverProfileDto(
    string? Bio,
    string? City,
    double? BaseLatitude,
    double? BaseLongitude,
    string? BaseAddress,
    int ServiceRadiusKm,
    string TimeZoneId,
    bool IsAcceptingRides,
    bool IsVerified,
    double RatingAverage,
    int RatingCount,
    int CompletedRides)
{
    public static DriverProfileDto From(JoyDriverProfile p) => new(
        p.Bio, p.City, p.BaseLatitude, p.BaseLongitude, p.BaseAddress, p.ServiceRadiusKm, p.TimeZoneId,
        p.IsAcceptingRides, p.IsVerified, p.RatingAverage, p.RatingCount, p.CompletedRides);
}

/// <summary>Small driver card shown next to a car.</summary>
public record DriverSummaryDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    string? City,
    bool IsVerified,
    double RatingAverage,
    int RatingCount)
{
    public static DriverSummaryDto From(JoyDriverProfile p) => new(
        p.UserId, UserNames.Display(p.User), p.User.AvatarUrl, p.City, p.IsVerified, p.RatingAverage, p.RatingCount);
}

/// <summary>What a rider sees when opening a driver's profile.</summary>
public record PublicDriverProfileDto(
    DriverSummaryDto Driver,
    string? Bio,
    int CompletedRides,
    DateTimeOffset MemberSince,
    IReadOnlyList<CarSummaryDto> Cars,
    IReadOnlyList<ReviewDto> RecentReviews);

public record UpdateDriverProfileRequest(
    [property: MaxLength(2000)] string? Bio,
    [property: MaxLength(100)] string? City,
    [property: Range(-90, 90)] double? BaseLatitude,
    [property: Range(-180, 180)] double? BaseLongitude,
    [property: MaxLength(300)] string? BaseAddress,
    [property: Range(0, 500)] int ServiceRadiusKm,
    [property: Required, MaxLength(64)] string TimeZoneId,
    bool IsAcceptingRides);
