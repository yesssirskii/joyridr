using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

/// <summary>Review queue that keeps the platform sports/supercar only, and the moderation queue for reports.</summary>
public class AdminService(AppDbContext db, CarService cars, RideService rides, NotificationService notifications, TimeProvider clock)
{
    public async Task<List<CarDetailDto>> ListCarsAsync(CarStatus status, CancellationToken ct)
    {
        var list = await db.Cars
            .Include(c => c.Photos)
            .Include(c => c.Driver).ThenInclude(d => d.User)
            .Where(c => c.Status == status && c.DeletedAt == null)
            .OrderBy(c => c.UpdatedAt)
            .AsSplitQuery()
            .ToListAsync(ct);
        return list.Select(c => CarDetailDto.From(c, isOwner: true)).ToList();
    }

    public async Task<CarDetailDto> ApproveCarAsync(Guid carId, CancellationToken ct)
    {
        var car = await cars.LoadCarAsync(carId, ct);
        car.Status = CarStatus.Approved;
        car.RejectionReason = null;
        notifications.Add(car.DriverId, NotificationType.CarApproved, "Your car is live! 🎉",
            $"Your {car.Make} {car.Model} was approved and riders can now book it.", carId: car.Id);
        await db.SaveChangesAsync(ct);
        await notifications.SendPushesAsync(ct);
        return CarDetailDto.From(car, isOwner: true);
    }

    public async Task<CarDetailDto> RejectCarAsync(Guid carId, string? reason, CancellationToken ct)
    {
        var car = await cars.LoadCarAsync(carId, ct);
        car.Status = CarStatus.Rejected;
        car.RejectionReason = reason?.Trim();
        notifications.Add(car.DriverId, NotificationType.CarRejected, "Your car wasn't approved",
            reason ?? $"Your {car.Make} {car.Model} doesn't meet our sports/supercar requirements.", carId: car.Id);
        await db.SaveChangesAsync(ct);
        await notifications.SendPushesAsync(ct);
        return CarDetailDto.From(car, isOwner: true);
    }

    public async Task VerifyDriverAsync(Guid driverId, CancellationToken ct)
    {
        var profile = await db.DriverProfiles.FirstOrDefaultAsync(d => d.UserId == driverId, ct) ?? throw ApiException.NotFound("Driver");
        profile.IsVerified = true;
        await db.SaveChangesAsync(ct);
    }

    // ---- Reports ----

    /// <summary>Reports with the given status.</summary>
    public async Task<List<ReportDto>> ListReportsAsync(ReportStatus status, CancellationToken ct)
    {
        var query = db.Reports.Where(r => r.Status == status);
        // Open: oldest first, so nothing waits too long. Handled: most recently handled first.
        query = status == ReportStatus.Open ? query.OrderBy(r => r.CreatedAt) : query.OrderByDescending(r => r.ResolvedAt);
        var reports = await query
            .Take(200)
            .Include(r => r.Reporter)
            .Include(r => r.ReportedUser)
            .AsSplitQuery()
            .ToListAsync(ct);

        var reportedIds = reports.Select(r => r.ReportedUserId).Distinct().ToList();
        var openCounts = await db.Reports
            .Where(r => r.Status == ReportStatus.Open && reportedIds.Contains(r.ReportedUserId))
            .GroupBy(r => r.ReportedUserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        return reports.Select(r => new ReportDto(
            r.Id, r.Kind, r.Reason, r.Details, r.ContentSnapshot,
            ContentExists: r.MessageId is not null || r.ReviewId is not null,
            ReportUserDto.From(r.Reporter), ReportUserDto.From(r.ReportedUser),
            openCounts.GetValueOrDefault(r.ReportedUserId),
            r.Status, r.CreatedAt, r.ResolvedAt, r.Resolution)).ToList();
    }

    public async Task DismissReportAsync(Guid adminId, Guid reportId, CancellationToken ct)
    {
        var report = await LoadOpenReportAsync(reportId, ct);
        Close(report, ReportStatus.Dismissed, "Dismissed", adminId);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Deletes the reported message or review, and closes every open report about it.</summary>
    public async Task RemoveReportedContentAsync(Guid adminId, Guid reportId, CancellationToken ct)
    {
        var report = await LoadOpenReportAsync(reportId, ct);
        var now = clock.GetUtcNow();

        if (report.MessageId is { } messageId)
        {
            await CloseOpenReportsAsync(r => r.MessageId == messageId, "Message removed", adminId, now, ct);
            await db.ChatMessages.Where(m => m.Id == messageId).ExecuteDeleteAsync(ct);
        }
        else if (report.ReviewId is { } reviewId)
        {
            var review = await db.Reviews.FirstAsync(r => r.Id == reviewId, ct);
            await CloseOpenReportsAsync(r => r.ReviewId == reviewId, "Review removed", adminId, now, ct);
            db.Reviews.Remove(review);
            await db.SaveChangesAsync(ct);
            await RecalculateRatingAsync(review.SubjectId, ct);
        }
        else
        {
            throw ApiException.Conflict("There's nothing left to remove for this report.", "nothing_to_remove");
        }
    }

    /// <summary>
    /// Logs the user out everywhere and stops them logging in. Their upcoming rides are cancelled with a
    /// full refund to the rider, a driver's cars are taken off the app, and open reports about them are closed.
    /// </summary>
    public async Task BanUserAsync(Guid adminId, Guid userId, string? reason, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.DriverProfile).FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw ApiException.NotFound("User");
        if (user.Role == UserRole.Admin)
            throw ApiException.BadRequest("Admins can't be banned here.", "ban_admin");
        if (user.BannedAt is not null)
            return;

        await rides.CloseOpenRidesAsync(userId, fullRefund: true, "This account was suspended.", ct);

        var now = clock.GetUtcNow();
        user.BannedAt = now;
        user.BanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (user.DriverProfile is { } driver)
        {
            driver.IsAcceptingRides = false;
            await db.Cars.Where(c => c.DriverId == userId).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsActive, false), ct);
        }
        await db.SaveChangesAsync(ct);

        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        await db.DeviceTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await CloseOpenReportsAsync(r => r.ReportedUserId == userId, "User banned", adminId, now, ct);
    }

    private async Task<Report> LoadOpenReportAsync(Guid reportId, CancellationToken ct)
    {
        var report = await db.Reports.FirstOrDefaultAsync(r => r.Id == reportId, ct) ?? throw ApiException.NotFound("Report");
        if (report.Status != ReportStatus.Open)
            throw ApiException.Conflict("This report has already been handled.", "report_closed");
        return report;
    }

    private void Close(Report report, ReportStatus status, string resolution, Guid adminId)
    {
        report.Status = status;
        report.Resolution = resolution;
        report.ResolvedAt = clock.GetUtcNow();
        report.ResolvedById = adminId;
    }

    private Task CloseOpenReportsAsync(
        System.Linq.Expressions.Expression<Func<Report, bool>> which, string resolution, Guid adminId, DateTimeOffset now, CancellationToken ct) =>
        db.Reports.Where(r => r.Status == ReportStatus.Open).Where(which).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, ReportStatus.Resolved)
            .SetProperty(r => r.Resolution, resolution)
            .SetProperty(r => r.ResolvedAt, now)
            .SetProperty(r => r.ResolvedById, adminId), ct);

    /// <summary>Works the rating out again from the reviews that are left, after one was removed.</summary>
    private async Task RecalculateRatingAsync(Guid subjectId, CancellationToken ct)
    {
        var ratings = await db.Reviews.Where(r => r.SubjectId == subjectId).Select(r => r.Rating).ToListAsync(ct);
        var average = ratings.Count == 0 ? 0 : Math.Round(ratings.Average(), 2);
        await db.DriverProfiles.Where(p => p.UserId == subjectId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.RatingAverage, average).SetProperty(p => p.RatingCount, ratings.Count), ct);
        await db.RiderProfiles.Where(p => p.UserId == subjectId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.RatingAverage, average).SetProperty(p => p.RatingCount, ratings.Count), ct);
    }
}
