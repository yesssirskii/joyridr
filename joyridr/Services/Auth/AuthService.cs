using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Joyridr.Services;

public class AuthService(
    AppDbContext db,
    TokenService tokens,
    IPasswordHasher<User> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider clock)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (request.Role == UserRole.Admin)
            throw ApiException.BadRequest("You can only sign up as a JoyRider or a JoyDriver.", "invalid_role");

        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw ApiException.Conflict("An account with this email already exists.", "email_taken");

        var user = new User
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = request.Role,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        if (user.Role == UserRole.JoyRider)
            user.RiderProfile = new JoyRiderProfile();
        else
            user.DriverProfile = new JoyDriverProfile();

        db.Users.Add(user);
        var response = IssueTokens(user, familyId: Guid.NewGuid());

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw ApiException.Conflict("An account with this email already exists.", "email_taken");
        }

        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        var result = user is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (user is null || result == PasswordVerificationResult.Failed)
            throw ApiException.Unauthorized("Wrong email or password.", "invalid_credentials");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var response = IssueTokens(user, familyId: Guid.NewGuid());
        await db.SaveChangesAsync(ct);
        return response;
    }

    /// <summary>Exchanges a refresh token for a new access + refresh token pair (rotation).</summary>
    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var hash = TokenService.Hash(refreshToken);
        var existing = await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == hash, ct)
                       ?? throw ApiException.Unauthorized("Invalid refresh token.", "invalid_refresh_token");

        if (existing.RevokedAt is not null)
        {
            // A token that was already rotated is being reused: assume it was stolen and kill the whole session.
            await db.RefreshTokens
                .Where(t => t.FamilyId == existing.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
            throw ApiException.Unauthorized("Invalid refresh token.", "invalid_refresh_token");
        }

        if (existing.ExpiresAt <= now)
            throw ApiException.Unauthorized("Refresh token expired, please log in again.", "refresh_token_expired");

        var response = IssueTokens(existing.User, existing.FamilyId, out var newToken);
        existing.RevokedAt = now;
        existing.ReplacedByTokenId = newToken.Id;
        await db.SaveChangesAsync(ct);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.Hash(refreshToken);
        var now = clock.GetUtcNow();
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private AuthResponse IssueTokens(User user, Guid familyId) => IssueTokens(user, familyId, out _);

    private AuthResponse IssueTokens(User user, Guid familyId, out RefreshToken refreshTokenEntity)
    {
        var now = clock.GetUtcNow();
        var (accessToken, accessExpiresAt) = tokens.CreateAccessToken(user);
        var rawRefreshToken = TokenService.GenerateRefreshToken();

        refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = TokenService.Hash(rawRefreshToken),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays),
        };
        db.RefreshTokens.Add(refreshTokenEntity);

        return new AuthResponse(accessToken, accessExpiresAt, rawRefreshToken, refreshTokenEntity.ExpiresAt, UserDto.From(user));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
