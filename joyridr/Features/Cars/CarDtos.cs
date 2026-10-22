using System.ComponentModel.DataAnnotations;
using Joyridr.Domain;
using Joyridr.Features.Drivers;

namespace Joyridr.Features.Cars;

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
/// One result of the nearby search. The location is the driver's base rounded to ~1 km, so it's
/// good enough to put a pin on a map later without revealing anyone's home address.
/// </summary>
public record NearbyCarDto(
    CarSummaryDto Car,
    DriverSummaryDto Driver,
    double DistanceKm,
    double ApproximateLatitude,
    double ApproximateLongitude);

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
    [property: Required, MaxLength(60)] string Make,
    [property: Required, MaxLength(100)] string Model,
    [property: Range(1950, 2100)] int Year,
    [property: Required, MaxLength(40)] string Color,
    CarCategory Category,
    Transmission Transmission,
    [property: Range(1, 3000)] int Horsepower,
    [property: Range(1.0, 30.0)] decimal? ZeroToHundredSeconds,
    [property: Range(50, 600)] int? TopSpeedKmh,
    [property: Range(1, 8)] int PassengerSeats,
    [property: MaxLength(2000)] string? Description,
    [property: MaxLength(20)] string? LicensePlate,
    [property: Range(1.0, 100000.0)] decimal PricePerHour,
    [property: RegularExpression("^[A-Z]{3}$")] string? Currency,
    bool IsActive = true);
