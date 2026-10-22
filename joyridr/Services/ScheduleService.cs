using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Joyridr.Services;

public class ScheduleService(
    AppDbContext db,
    AvailabilityService availability,
    CarService cars,
    IOptions<RideOptions> rideOptions,
    TimeProvider clock)
{
    public async Task<WeeklyScheduleDto> GetWeeklyScheduleAsync(Guid driverId, CancellationToken ct)
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

    /// <summary>Replaces the whole weekly schedule. Rides that were already booked are not affected.</summary>
    public async Task<WeeklyScheduleDto> ReplaceWeeklyScheduleAsync(Guid driverId, UpdateWeeklyScheduleRequest request, CancellationToken ct)
    {
        var error = ScheduleCalculator.Validate(
            request.Windows.Select(w => (w.DayOfWeek, new TimeWindow(w.StartTime, w.EndTime))), rideOptions.Value.StepMinutes);
        if (error is not null)
            throw ApiException.BadRequest(error, "invalid_schedule");

        await db.DriverWeeklyAvailability.Where(a => a.DriverId == driverId).ExecuteDeleteAsync(ct);
        db.DriverWeeklyAvailability.AddRange(request.Windows.Select(w => new DriverWeeklyAvailability
        {
            DriverId = driverId,
            DayOfWeek = w.DayOfWeek,
            StartTime = w.StartTime,
            EndTime = w.EndTime,
        }));
        await db.SaveChangesAsync(ct);
        return await GetWeeklyScheduleAsync(driverId, ct);
    }

    public async Task<List<TimeOffDto>> GetUpcomingTimeOffAsync(Guid driverId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await db.DriverTimeOff
            .Where(t => t.DriverId == driverId && t.EndsAt > now)
            .OrderBy(t => t.StartsAt)
            .Select(t => new TimeOffDto(t.Id, t.StartsAt, t.EndsAt, t.Reason))
            .ToListAsync(ct);
    }

    public async Task<TimeOffDto> AddTimeOffAsync(Guid driverId, CreateTimeOffRequest request, CancellationToken ct)
    {
        var startsAt = request.StartsAt.ToUniversalTime();
        var endsAt = request.EndsAt.ToUniversalTime();
        if (endsAt <= startsAt)
            throw ApiException.BadRequest("Time off must end after it starts.", "invalid_time_off");

        var clashes = await db.Rides.AnyAsync(r => r.DriverId == driverId && Ride.BlockingStatuses.Contains(r.Status)
                                                   && r.StartsAt < endsAt && startsAt < r.EndsAt, ct);
        if (clashes)
            throw ApiException.Conflict("You have rides during that time. Decline or cancel them first.", "time_off_clashes_with_rides");

        var timeOff = new DriverTimeOff { DriverId = driverId, StartsAt = startsAt, EndsAt = endsAt, Reason = request.Reason?.Trim() };
        db.DriverTimeOff.Add(timeOff);
        await db.SaveChangesAsync(ct);
        return new TimeOffDto(timeOff.Id, timeOff.StartsAt, timeOff.EndsAt, timeOff.Reason);
    }

    public async Task DeleteTimeOffAsync(Guid driverId, Guid timeOffId, CancellationToken ct)
    {
        var deleted = await db.DriverTimeOff.Where(t => t.Id == timeOffId && t.DriverId == driverId).ExecuteDeleteAsync(ct);
        if (deleted == 0)
            throw ApiException.NotFound("Time off");
    }

    /// <summary>Start times a car can be booked at on a date in the driver's time zone.</summary>
    public async Task<SlotsResponse> GetSlotsAsync(Guid carId, DateOnly date, int durationMinutes, CancellationToken ct)
    {
        var car = await cars.LoadCarAsync(carId, ct);
        if (!car.IsListed || !car.Driver.IsAcceptingRides)
            throw ApiException.NotFound("Car");

        var slots = await availability.GetSlotsAsync(car.Driver, date, durationMinutes, ct);
        return new SlotsResponse(car.Id, date, car.Driver.TimeZoneId, durationMinutes,
            RidePricing.Calculate(car.PricePerHour, durationMinutes), car.Currency, slots);
    }
}
