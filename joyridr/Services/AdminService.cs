using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

/// <summary>Review queue that keeps the platform sports/supercar only.</summary>
public class AdminService(AppDbContext db, CarService cars, NotificationService notifications)
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
}
