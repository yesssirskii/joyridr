using Joyridr.Common;
using Joyridr.Data;
using Joyridr.Domain;
using Joyridr.Features.Cars;
using Joyridr.Features.Notifications;
using Joyridr.Features.Rides;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Features.Admin;

/// <summary>Review queue that keeps the platform sports/supercar only.</summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization(Policies.Admin);

        group.MapGet("/cars", async (AppDbContext db, CancellationToken ct, string? status) =>
        {
            var carStatus = QueryEnum.Parse<CarStatus>(status, nameof(status)) ?? CarStatus.PendingReview;
            var cars = await db.Cars
                .Include(c => c.Photos)
                .Include(c => c.Driver).ThenInclude(d => d.User)
                .Where(c => c.Status == carStatus && c.DeletedAt == null)
                .OrderBy(c => c.UpdatedAt)
                .AsSplitQuery()
                .ToListAsync(ct);
            return cars.Select(c => CarDetailDto.From(c, isOwner: true)).ToList();
        });

        group.MapPost("/cars/{carId:guid}/approve", async (Guid carId, AppDbContext db, NotificationService notifications, CancellationToken ct) =>
        {
            var car = await CarEndpoints.LoadCar(db, carId, ct);
            car.Status = CarStatus.Approved;
            car.RejectionReason = null;
            notifications.Add(car.DriverId, NotificationType.CarApproved, "Your car is live! 🎉",
                $"Your {car.Make} {car.Model} was approved and riders can now book it.", carId: car.Id);
            await db.SaveChangesAsync(ct);
            await notifications.SendPushesAsync(ct);
            return CarDetailDto.From(car, isOwner: true);
        });

        group.MapPost("/cars/{carId:guid}/reject", async (Guid carId, ReasonRequest request, AppDbContext db, NotificationService notifications, CancellationToken ct) =>
        {
            var car = await CarEndpoints.LoadCar(db, carId, ct);
            car.Status = CarStatus.Rejected;
            car.RejectionReason = request.Reason?.Trim();
            notifications.Add(car.DriverId, NotificationType.CarRejected, "Your car wasn't approved",
                request.Reason ?? $"Your {car.Make} {car.Model} doesn't meet our sports/supercar requirements.", carId: car.Id);
            await db.SaveChangesAsync(ct);
            await notifications.SendPushesAsync(ct);
            return CarDetailDto.From(car, isOwner: true);
        });

        group.MapPost("/drivers/{driverId:guid}/verify", async (Guid driverId, AppDbContext db, CancellationToken ct) =>
        {
            var profile = await db.DriverProfiles.FirstOrDefaultAsync(d => d.UserId == driverId, ct) ?? throw ApiException.NotFound("Driver");
            profile.IsVerified = true;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Mark a driver's licence/identity as checked.");

        return app;
    }
}
