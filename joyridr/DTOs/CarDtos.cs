using System.ComponentModel.DataAnnotations;
using Joyridr.Models;
using Joyridr.Models.Enums;

namespace Joyridr.DTOs;

public record CarPhotoDto(Guid Id, string Url, int SortOrder)
{
    public static CarPhotoDto From(CarPhoto p) => new(p.Id, p.Url, p.SortOrder);
}

/// <summary>Compact car info for lists.</summary>
public record CarSummaryDto(
    Guid Id,
    string Make,
    string Model,
    int Year,
    string Color,
    CarCategory Category,
    Transmission Transmission,
    int Horsepower,
    int PassengerSeats,
    decimal PricePerHour,
    string Currency,
    string? PhotoUrl)
{
    public static CarSummaryDto From(Car c) => new(
        c.Id, c.Make, c.Model, c.Year, c.Color, c.Category, c.Transmission, c.Horsepower, c.PassengerSeats,
        c.PricePerHour, c.Currency, c.Photos.OrderBy(p => p.SortOrder).FirstOrDefault()?.Url);
}

/// <summary>
/// One result of the nearby search (one per car, so a driver with two cars appears twice).
/// The location is the driver's base rounded to ~1 km, so it's good enough to put a pin on a map
/// later without revealing anyone's home address.
/// </summary>
public record NearbyCarDto(
    CarSummaryDto Car,
    DriverSummaryDto Driver,
    double DistanceKm,
    double ApproximateLatitude,
    double ApproximateLongitude,
    // The green dot: the driver still has a free slot today (in their time zone).
    bool IsAvailableToday,
    // Start of the driver's next free slot within a week, e.g. for "Available tomorrow 10:00". Null if none.
    DateTimeOffset? NextAvailableAt);

public record CarDetailDto(
    Guid Id,
    string Make,
    string Model,
    int Year,
    string Color,
    CarCategory Category,
    Transmission Transmission,
    int Horsepower,
    decimal? ZeroToHundredSeconds,
    int? TopSpeedKmh,
    int PassengerSeats,
    string? Description,
    decimal PricePerHour,
    string Currency,
    IReadOnlyList<CarPhotoDto> Photos,
    DriverSummaryDto Driver,
    CarStatus Status,
    bool IsActive,
    // Only filled in for the car's owner:
    string? LicensePlate,
    string? RejectionReason)
{
    public static CarDetailDto From(Car c, bool isOwner) => new(
        c.Id, c.Make, c.Model, c.Year, c.Color, c.Category, c.Transmission, c.Horsepower, c.ZeroToHundredSeconds,
        c.TopSpeedKmh, c.PassengerSeats, c.Description, c.PricePerHour, c.Currency,
        c.Photos.OrderBy(p => p.SortOrder).Select(CarPhotoDto.From).ToList(),
        DriverSummaryDto.From(c.Driver), c.Status, c.IsActive,
        isOwner ? c.LicensePlate : null,
        isOwner ? c.RejectionReason : null);
}

public record UpsertCarRequest(
    [Required, MaxLength(60)] string Make,
    [Required, MaxLength(100)] string Model,
    [Range(1950, 2100)] int Year,
    [Required, MaxLength(40)] string Color,
    CarCategory Category,
    Transmission Transmission,
    [Range(1, 3000)] int Horsepower,
    [Range(1.0, 30.0)] decimal? ZeroToHundredSeconds,
    [Range(50, 600)] int? TopSpeedKmh,
    [Range(1, 8)] int PassengerSeats,
    [MaxLength(2000)] string? Description,
    [MaxLength(20)] string? LicensePlate,
    [Range(1.0, 100000.0)] decimal PricePerHour,
    [RegularExpression("^[A-Z]{3}$")] string? Currency,
    bool IsActive = true);

/// <summary>Filters for the nearby search. Built by the controller from query parameters.</summary>
public record NearbyCarsQuery(
    double Latitude,
    double Longitude,
    double RadiusKm,
    CarCategory? Category,
    int? MinSeats,
    decimal? MaxPricePerHour,
    int Page,
    int PageSize);
