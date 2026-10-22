using System.Security.Claims;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.Domain;
using Joyridr.Features.Cars;
using Joyridr.Features.Rides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Joyridr.Features.Schedule;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var drivers = app.MapGroup("/api/drivers").WithTags("Schedule").RequireAuthorization();

        drivers.MapGet("/me/schedule", (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
            GetWeeklySchedule(principal.GetUserId(), db, ct)).RequireAuthorization(Policies.Driver);

        drivers.MapPut("/me/schedule", async (UpdateWeeklyScheduleRequest request, ClaimsPrincipal principal, AppDbContext db, IOptions<RideOptions> rideOptions, CancellationToken ct) =>
        {
            var error = ScheduleCalculator.Validate(
                request.Windows.Select(w => (w.DayOfWeek, new TimeWindow(w.StartTime, w.EndTime))), rideOptions.Value.StepMinutes);
            if (error is not null)
                throw ApiException.BadRequest(error, "invalid_schedule");

            // Replaces the whole weekly schedule. Rides that were already booked are not affected.
            var driverId = principal.GetUserId();
            await db.DriverWeeklyAvailability.Where(a => a.DriverId == driverId).ExecuteDeleteAsync(ct);
            db.DriverWeeklyAvailability.AddRange(request.Windows.Select(w => new DriverWeeklyAvailability
            {
                DriverId = driverId,
                DayOfWeek = w.DayOfWeek,
                StartTime = w.StartTime,
                EndTime = w.EndTime,
            }));
            await db.SaveChangesAsync(ct);
            return await GetWeeklySchedule(driverId, db, ct);
        }).RequireAuthorization(Policies.Driver).WithSummary("Replace the driver's whole weekly schedule.");

        drivers.MapGet("/{driverId:guid}/schedule", (Guid driverId, AppDbContext db, CancellationToken ct) =>
            GetWeeklySchedule(driverId, db, ct)).WithSummary("A driver's weekly schedule (in their time zone).");

        drivers.MapGet("/me/time-off", async (ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var driverId = principal.GetUserId();
            var now = clock.GetUtcNow();
            return await db.DriverTimeOff
                .Where(t => t.DriverId == driverId && t.EndsAt > now)
                .OrderBy(t => t.StartsAt)
                .Select(t => new TimeOffDto(t.Id, t.StartsAt, t.EndsAt, t.Reason))
                .ToListAsync(ct);
        }).RequireAuthorization(Policies.Driver);

        drivers.MapPost("/me/time-off", async (CreateTimeOffRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var startsAt = request.StartsAt.ToUniversalTime();
            var endsAt = request.EndsAt.ToUniversalTime();
            if (endsAt <= startsAt)
                throw ApiException.BadRequest("Time off must end after it starts.", "invalid_time_off");

            var driverId = principal.GetUserId();
            var clashes = await db.Rides.AnyAsync(r => r.DriverId == driverId && Ride.BlockingStatuses.Contains(r.Status)
                                                       && r.StartsAt < endsAt && startsAt < r.EndsAt, ct);
            if (clashes)
                throw ApiException.Conflict("You have rides during that time. Decline or cancel them first.", "time_off_clashes_with_rides");

            var timeOff = new DriverTimeOff { DriverId = driverId, StartsAt = startsAt, EndsAt = endsAt, Reason = request.Reason?.Trim() };
            db.DriverTimeOff.Add(timeOff);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/drivers/me/time-off/{timeOff.Id}", new TimeOffDto(timeOff.Id, timeOff.StartsAt, timeOff.EndsAt, timeOff.Reason));
        }).RequireAuthorization(Policies.Driver);

        drivers.MapDelete("/me/time-off/{timeOffId:guid}", async (Guid timeOffId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var driverId = principal.GetUserId();
            var deleted = await db.DriverTimeOff.Where(t => t.Id == timeOffId && t.DriverId == driverId).ExecuteDeleteAsync(ct);
            return deleted == 0 ? throw ApiException.NotFound("Time off") : Results.NoContent();
        }).RequireAuthorization(Policies.Driver);

        app.MapGet("/api/cars/{carId:guid}/slots", async (Guid carId, DateOnly date, int durationMinutes, AppDbContext db, AvailabilityService availability, CancellationToken ct) =>
            {
                var car = await CarEndpoints.LoadCar(db, carId, ct);
                if (!car.IsListed || !car.Driver.IsAcceptingRides)
                    throw ApiException.NotFound("Car");

                var slots = await availability.GetSlotsAsync(car.Driver, date, durationMinutes, ct);
                return new SlotsResponse(car.Id, date, car.Driver.TimeZoneId, durationMinutes,
                    RidePricing.Calculate(car.PricePerHour, durationMinutes), car.Currency, slots);
            })
            .WithTags("Schedule")
            .RequireAuthorization()
            .WithSummary("Start times a car can be booked at on a given date (date is in the driver's time zone).");

        return app;
    }

    private static async Task<WeeklyScheduleDto> GetWeeklySchedule(Guid driverId, AppDbContext db, CancellationToken ct)
    {
        var profile = await db.DriverProfiles.FirstOrDefaultAsync(d => d.UserId == driverId, ct)
                      ?? throw ApiException.NotFound("Driver");
        var windows = await db.DriverWeeklyAvailability
            .Where(a => a.DriverId == driverId)
            .OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
            .Select(a => new WeeklyWindowDto(a.DayOfWeek, a.StartTime, a.EndTime))
            .ToListAsync(ct);
        return new WeeklyScheduleDto(profile.TimeZoneId, windows);
    }
}
