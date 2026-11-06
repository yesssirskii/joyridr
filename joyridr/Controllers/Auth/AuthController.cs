using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Joyridr.Controllers;

[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicy)]
public class AuthController(AuthService auth) : ApiControllerBase
{
    public const string RateLimitPolicy = "auth";

    /// <summary>Create an account as a JoyRider or JoyDriver. Returns tokens, so the user is logged in straight away.</summary>
    [HttpPost("register")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken ct) => auth.RegisterAsync(request, ct);

    /// <summary>Log in with email and password.</summary>
    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);

    /// <summary>Swap a refresh token for a new token pair. The old refresh token stops working.</summary>
    [HttpPost("refresh")]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct) => auth.RefreshAsync(request.RefreshToken, ct);

    /// <summary>
    /// Email a 6-digit code for resetting the password. Always answers 204, whether or not the email has an
    /// account, so it can't be used to find out who's signed up. One code per minute at most.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request.Email, ct);
        return NoContent();
    }

    /// <summary>
    /// Set a new password with the emailed code. Logs out every other device and returns new tokens, so the
    /// user is logged in straight away. Fails with "invalid_code", "code_expired" or "too_many_attempts".
    /// </summary>
    [HttpPost("reset-password")]
    public Task<AuthResponse> ResetPassword(ResetPasswordRequest request, CancellationToken ct) => auth.ResetPasswordAsync(request, ct);

    /// <summary>Revoke a refresh token (log out on this device).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
