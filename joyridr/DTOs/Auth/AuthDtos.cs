using System.ComponentModel.DataAnnotations;
using Joyridr.Models.Enums;

namespace Joyridr.DTOs;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(32)] string Username,
    [Required, MinLength(8), MaxLength(128)] string Password,
    [Required, MaxLength(100)] string FirstName,
    [Required, MaxLength(100)] string LastName,
    [MaxLength(32)] string? PhoneNumber,
    UserRole Role);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record ForgotPasswordRequest([Required, EmailAddress, MaxLength(256)] string Email);

public record ResetPasswordRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "The code is the 6 digits from the email.")] string Code,
    [Required, MinLength(8, ErrorMessage = "Your new password needs at least 8 characters."), MaxLength(128)] string NewPassword);

public record AuthResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);
