using System.Security.Cryptography;
using System.Text;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Interfaces;
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
    EmailQueue emails,
    TimeProvider clock,
    ILogger<AuthService> logger)
{
    private static readonly TimeSpan ResetCodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResetCodeCooldown = TimeSpan.FromSeconds(60);
    private const int MaxResetAttempts = 5;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (request.Role == UserRole.Admin)
            throw ApiException.BadRequest("You can only sign up as a JoyRider or a JoyDriver.", "invalid_role");

        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw ApiException.Conflict("An account with this email already exists.", "email_taken");
        var username = UserNames.NormalizeUsername(request.Username);
        if (await db.Users.AnyAsync(u => u.Username == username, ct))
            throw UsernameTaken();

        var user = new User
        {
            Email = email,
            Username = username,
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
        // Someone took the email or username between the checks above and this save.
        catch (DbUpdateException ex) when (ex.IsUniqueViolation(UsernameIndex))
        {
            throw UsernameTaken();
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
        // Only after the password check, so the ban isn't revealed to someone guessing.
        if (user.BannedAt is not null)
            throw new ApiException(StatusCodes.Status403Forbidden, "This account has been suspended.", "account_banned");

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
        if (!existing.User.IsActive)
            throw ApiException.Unauthorized("This account is no longer active.", "account_inactive");

        var response = IssueTokens(existing.User, existing.FamilyId, out var newToken);
        existing.RevokedAt = now;
        existing.ReplacedByTokenId = newToken.Id;
        await db.SaveChangesAsync(ct);
        return response;
    }

    // ---- Forgot password ----

    public async Task ForgotPasswordAsync(string email, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized && u.DeletedAt == null && u.BannedAt == null, ct);
        if (user is null)
        {
            // Same answer to the app as for a real account; only the log says why nothing was sent.
            logger.LogInformation("Password reset asked for an email with no active account; no code sent.");
            return;
        }

        var now = clock.GetUtcNow();
        // A double tap or an impatient "resend" shouldn't flood the inbox (or our Resend quota).
        if (await db.PasswordResetCodes.AnyAsync(c => c.UserId == user.Id && c.CreatedAt > now - ResetCodeCooldown, ct))
        {
            logger.LogInformation("Password reset asked again within a minute for user {UserId}; no new code sent.", user.Id);
            return;
        }

        // Only the newest code works.
        await db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && c.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, now), ct);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        db.PasswordResetCodes.Add(new PasswordResetCode
        {
            UserId = user.Id,
            CodeHash = HashResetCode(user.Id, code),
            CreatedAt = now,
            ExpiresAt = now + ResetCodeLifetime,
        });
        await db.SaveChangesAsync(ct);

        emails.Enqueue(ResetCodeEmail(user, code));
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var email = NormalizeEmail(request.Email);
        var now = clock.GetUtcNow();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null && u.BannedAt == null, ct);
        var resetCode = user is null
            ? null
            : await db.PasswordResetCodes
                .Where(c => c.UserId == user.Id && c.UsedAt == null)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(ct);

        // No account and no code look the same as a wrong code.
        if (user is null || resetCode is null)
            throw InvalidCode();
        if (resetCode.ExpiresAt <= now)
            throw ApiException.BadRequest("That code has expired. Ask for a new one.", "code_expired");
        if (resetCode.Attempts >= MaxResetAttempts)
            throw TooManyAttempts();

        var expected = Encoding.ASCII.GetBytes(resetCode.CodeHash);
        var actual = Encoding.ASCII.GetBytes(HashResetCode(user.Id, request.Code));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            resetCode.Attempts++;
            await db.SaveChangesAsync(ct);
            throw resetCode.Attempts >= MaxResetAttempts ? TooManyAttempts() : InvalidCode();
        }

        resetCode.UsedAt = now;
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        // Whoever knew the old password is logged out everywhere.
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

        var response = IssueTokens(user, familyId: Guid.NewGuid());
        await db.SaveChangesAsync(ct);
        return response;
    }

    /// <summary>
    /// HMAC with a key derived from the JWT signing key. A plain hash of a 6-digit code could be reversed
    /// by trying all million codes, so a leaked database alone mustn't be enough.
    /// </summary>
    private string HashResetCode(Guid userId, string code)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes("password-reset:" + jwtOptions.Value.SigningKey));
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{userId:N}:{code}"));
        return Convert.ToHexStringLower(mac);
    }

    private static ApiException InvalidCode() =>
        ApiException.BadRequest("That code isn't right. Check the email and try again.", "invalid_code");

    private static ApiException TooManyAttempts() =>
        ApiException.BadRequest("Too many wrong tries. Ask for a new code.", "too_many_attempts");

    private static EmailMessage ResetCodeEmail(User user, string code)
    {
        var minutes = (int)ResetCodeLifetime.TotalMinutes;
        var name = System.Net.WebUtility.HtmlEncode(user.FirstName);
        var text = $"""
            Hi {user.FirstName},

            Your JOYRIDR password reset code is {code}

            Enter it in the app within {minutes} minutes. If you didn't ask to reset your password, you can ignore this email; your password stays the same.

            JOYRIDR
            """;
        var html = $"""
            <div style="font-family:-apple-system,Helvetica,Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#111">
              <p style="font-size:22px;font-weight:800;letter-spacing:2px;margin:0 0 24px">JOYRIDR</p>
              <p style="font-size:16px;margin:0 0 16px">Hi {name},</p>
              <p style="font-size:16px;margin:0 0 16px">Your password reset code is:</p>
              <p style="font-size:36px;font-weight:700;letter-spacing:8px;margin:0 0 16px;font-family:Menlo,Consolas,monospace">{code}</p>
              <p style="font-size:14px;color:#555;margin:0 0 8px">Enter it in the app within {minutes} minutes.</p>
              <p style="font-size:14px;color:#555;margin:0">If you didn't ask to reset your password, you can ignore this email; your password stays the same.</p>
            </div>
            """;
        return new EmailMessage(user.Email, $"Your JOYRIDR code: {code}", html, text);
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

    internal const string UsernameIndex = "ix_users_username";

    internal static ApiException UsernameTaken() =>
        ApiException.Conflict("That username is taken. Try another one.", "username_taken");
}
