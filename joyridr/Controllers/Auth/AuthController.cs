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

    /// <summary>Revoke a refresh token (log out on this device).</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request.RefreshToken, ct);
        return NoContent();
    }
}
