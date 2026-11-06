using System.Security.Cryptography;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.Interfaces;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

/// <summary>
/// Deleting an account, as Apple requires (App Review guideline 5.1.1(v)): every piece of personal data
/// and everything the user wrote goes. The user row itself stays, emptied, because rides and payments
/// point at it and payment records have to be kept for accounting.
/// </summary>
public class AccountService(
    AppDbContext db,
    RideService rides,
    IPasswordHasher<User> passwordHasher,
    IFileStorage storage,
    TimeProvider clock,
    ILogger<AccountService> logger)
{
    public async Task DeleteAsync(Guid userId, string password, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.RiderProfile)
            .Include(u => u.DriverProfile)
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null, ct)
            ?? throw ApiException.NotFound("User");

        // 400 rather than 401, so the app doesn't take it for an expired session and log the user out.
        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            throw ApiException.BadRequest("That password isn't right.", "wrong_password");

        if (await db.Rides.AnyAsync(r => (r.RiderId == userId || r.DriverId == userId) && r.Status == RideStatus.InProgress, ct))
            throw ApiException.Conflict("You have a ride in progress. You can delete your account once it's finished.", "ride_in_progress");

        // First, so the other people get told by name and the payment rules run as for a normal cancellation.
        await rides.CloseOpenRidesAsync(userId, fullRefund: false, "Account deleted.", ct);

        var now = clock.GetUtcNow();
        var filesToDelete = new List<string>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // ---- The user and their profile ----
        if (user.AvatarUrl is not null)
            filesToDelete.Add(user.AvatarUrl);
        user.Email = $"deleted-{user.Id:N}@deleted.invalid"; // frees the email for a new sign-up
        user.Username = $"deleted_{user.Id:N}"[..UserNames.UsernameMaxLength];
        user.FirstName = "Deleted";
        user.LastName = "user";
        user.PhoneNumber = null;
        user.AvatarUrl = null;
        // A password nobody knows, so the row can never be logged into.
        user.PasswordHash = passwordHasher.HashPassword(user, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        user.DeletedAt = now;

        if (user.RiderProfile is { } rider)
            rider.DateOfBirth = null;

        if (user.DriverProfile is { } driver)
        {
            driver.Bio = null;
            driver.City = null;
            driver.BaseAddress = null;
            driver.BaseLatitude = null;
            driver.BaseLongitude = null;
            driver.IsAcceptingRides = false;

            // Cars stay (past rides point at them) but are taken off the app; their photos and plates go.
            var cars = await db.Cars.Include(c => c.Photos).Where(c => c.DriverId == userId).ToListAsync(ct);
            foreach (var car in cars)
            {
                car.DeletedAt ??= now;
                car.IsActive = false;
                car.LicensePlate = null;
                filesToDelete.AddRange(car.Photos.Select(p => p.Url));
                db.CarPhotos.RemoveRange(car.Photos);
            }
            await db.DriverTimeOff.Where(t => t.DriverId == userId).ExecuteDeleteAsync(ct);
            await db.DriverWeeklyAvailability.Where(a => a.DriverId == userId).ExecuteDeleteAsync(ct);
        }

        await db.SaveChangesAsync(ct);

        // ---- What they wrote ----
        await db.ChatMessages.Where(m => m.SenderId == userId).ExecuteDeleteAsync(ct);
        // The star rating stays (without its author's name or text), so the other person's average doesn't change.
        await db.Reviews.Where(r => r.AuthorId == userId).ExecuteUpdateAsync(s => s.SetProperty(r => r.Comment, (string?)null), ct);
        // Their note to the driver and the meeting places (often a home address).
        await db.Rides.Where(r => r.RiderId == userId || r.DriverId == userId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.RiderNote, (string?)null)
            .SetProperty(r => r.MeetingAddress, (string?)null)
            .SetProperty(r => r.MeetingLatitude, (double?)null)
            .SetProperty(r => r.MeetingLongitude, (double?)null), ct);

        // ---- Sessions, devices, inbox, blocks ----
        await db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await db.DeviceTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync(ct);
        await db.UserBlocks.Where(b => b.BlockerId == userId || b.BlockedId == userId).ExecuteDeleteAsync(ct);

        // Reports about them have nothing left to act on.
        await db.Reports.Where(r => r.ReportedUserId == userId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.ContentSnapshot, (string?)null)
            .SetProperty(r => r.Status, r => r.Status == ReportStatus.Open ? ReportStatus.Resolved : r.Status)
            .SetProperty(r => r.Resolution, r => r.Status == ReportStatus.Open ? "Account deleted" : r.Resolution)
            .SetProperty(r => r.ResolvedAt, r => r.Status == ReportStatus.Open ? now : r.ResolvedAt), ct);

        await transaction.CommitAsync(ct);

        // Files last: if one fails to delete, the account is still gone.
        foreach (var path in filesToDelete)
        {
            try
            {
                await storage.DeleteAsync(path, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Couldn't delete {Path} for deleted user {UserId}", path, userId);
            }
        }
    }
}
