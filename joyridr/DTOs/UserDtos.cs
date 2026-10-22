using System.ComponentModel.DataAnnotations;
using Joyridr.Models;
using Joyridr.Models.Enums;

namespace Joyridr.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? AvatarUrl,
    UserRole Role,
    DateTimeOffset CreatedAt)
{
    public static UserDto From(User u) => new(u.Id, u.Email, u.FirstName, u.LastName, u.PhoneNumber, u.AvatarUrl, u.Role, u.CreatedAt);
}

public record RiderProfileDto(DateOnly? DateOfBirth, double RatingAverage, int RatingCount, int CompletedRides)
{
    public static RiderProfileDto From(JoyRiderProfile p) => new(p.DateOfBirth, p.RatingAverage, p.RatingCount, p.CompletedRides);
}

/// <summary>Everything the app needs right after login. Exactly one of the profiles is set, depending on the role.</summary>
public record MeResponse(UserDto User, RiderProfileDto? RiderProfile, DriverProfileDto? DriverProfile);

public record UpdateMeRequest(
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [MaxLength(32)] string? PhoneNumber);

public record UpdateRiderProfileRequest(DateOnly? DateOfBirth);
