using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Joyridr.Services;

public class AvailabilityService(AppDbContext db, IOptions<RideOptions> rideOptions, TimeProvider clock)
{
    private readonly RideOptions _options = rideOptions.Value;

    public async Task<List<TimeSlot>> GetSlotsAsync(JoyDriverProfile driver, DateOnly date, int durationMinutes, CancellationToken ct)
    {
        ValidateDuration(durationMinutes);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(driver.TimeZoneId);
        var (dayStart, dayEnd) = ScheduleCalculator.DayBoundsUtc(date, timeZone);

        var now = clock.GetUtcNow();
        if (dayEnd <= now || dayStart > now.AddDays(_options.MaxAdvanceDays))
            return [];

        var windows = await GetWindowsAsync(driver.UserId, date.DayOfWeek, ct);
        var busy = await GetBusyAsync(driver.UserId, dayStart, dayEnd, ct);
        return ScheduleCalculator.GetSlots(date, timeZone, windows, busy, EarliestStart(now), durationMinutes, _options.StepMinutes);
    }

    /// <summary>
    /// When each driver can next take a (shortest possible) ride, looking a week ahead. Used for the
    /// green "available" dot in the nearby list. Loads everything for all drivers in three queries.
    /// </summary>
    public async Task<Dictionary<Guid, DriverAvailability>> GetAvailabilityAsync(IReadOnlyCollection<JoyDriverProfile> drivers, CancellationToken ct)
    {
        const int daysAhead = 7;
        var now = clock.GetUtcNow();
        var horizon = now.AddDays(daysAhead + 1);
        var ids = drivers.Select(d => d.UserId).Distinct().ToList();

        var windows = await db.DriverWeeklyAvailability
            .Where(a => ids.Contains(a.DriverId))
            .ToListAsync(ct);
        var rides = await db.Rides
            .Where(r => ids.Contains(r.DriverId) && Ride.BlockingStatuses.Contains(r.Status) && r.EndsAt > now && r.StartsAt < horizon)
            .Select(r => new { r.DriverId, r.StartsAt, r.EndsAt })
            .ToListAsync(ct);
        var timeOff = await db.DriverTimeOff
            .Where(t => ids.Contains(t.DriverId) && t.EndsAt > now && t.StartsAt < horizon)
            .Select(t => new { t.DriverId, t.StartsAt, t.EndsAt })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, DriverAvailability>();
        foreach (var driver in drivers.DistinctBy(d => d.UserId))
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(driver.TimeZoneId);
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timeZone).DateTime);
            var weekly = windows
                .Where(w => w.DriverId == driver.UserId)
                .GroupBy(w => w.DayOfWeek)
                .ToDictionary(g => g.Key, g => g.Select(w => new TimeWindow(w.StartTime, w.EndTime)).ToList());
            List<TimeSlot> busy =
            [
                .. rides.Where(r => r.DriverId == driver.UserId).Select(r => new TimeSlot(r.StartsAt, r.EndsAt)),
                .. timeOff.Where(t => t.DriverId == driver.UserId).Select(t => new TimeSlot(t.StartsAt, t.EndsAt)),
            ];

            var next = ScheduleCalculator.FindNextSlot(today, timeZone, weekly, busy, EarliestStart(now),
                _options.MinDurationMinutes, _options.StepMinutes, daysAhead);
            var isToday = next is not null && DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(next.StartsAt, timeZone).DateTime) == today;
            result[driver.UserId] = new DriverAvailability(isToday, next?.StartsAt);
        }
        return result;
    }

    /// <summary>Throws a 409 unless the driver can take a ride at exactly this time.</summary>
    public async Task EnsureBookableAsync(JoyDriverProfile driver, DateTimeOffset startsAt, int durationMinutes, CancellationToken ct)
    {
        ValidateDuration(durationMinutes);
        var now = clock.GetUtcNow();
        var slot = new TimeSlot(startsAt, startsAt.AddMinutes(durationMinutes));

        if (slot.StartsAt < EarliestStart(now))
            throw ApiException.BadRequest($"Rides must be booked at least {_options.MinLeadTimeMinutes} minutes in advance.", "too_soon");
        if (slot.StartsAt > now.AddDays(_options.MaxAdvanceDays))
            throw ApiException.BadRequest($"Rides can be booked at most {_options.MaxAdvanceDays} days in advance.", "too_far_ahead");

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(driver.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTime(slot.StartsAt, timeZone).DateTime;
        if (!ScheduleCalculator.IsOnGrid(TimeOnly.FromDateTime(localStart), _options.StepMinutes))
            throw ApiException.BadRequest($"Rides must start on a {_options.StepMinutes}-minute boundary. Use one of the slots from the slots endpoint.", "invalid_start_time");

        var localDate = DateOnly.FromDateTime(localStart);
        var windows = await GetWindowsAsync(driver.UserId, localDate.DayOfWeek, ct);
        if (!ScheduleCalculator.IsWithinWindows(slot, timeZone, windows))
            throw ApiException.Conflict("The driver isn't available at that time.", "slot_unavailable");

        var busy = await GetBusyAsync(driver.UserId, slot.StartsAt, slot.EndsAt, ct);
        if (busy.Count > 0)
            throw ApiException.Conflict("That time slot is already taken.", "slot_unavailable");
    }

    private void ValidateDuration(int durationMinutes)
    {
        if (durationMinutes < _options.MinDurationMinutes || durationMinutes > _options.MaxDurationMinutes || durationMinutes % _options.StepMinutes != 0)
            throw ApiException.BadRequest(
                $"Duration must be between {_options.MinDurationMinutes} and {_options.MaxDurationMinutes} minutes, in steps of {_options.StepMinutes}.",
                "invalid_duration");
    }

    private DateTimeOffset EarliestStart(DateTimeOffset now) => now.AddMinutes(_options.MinLeadTimeMinutes);

    private async Task<List<TimeWindow>> GetWindowsAsync(Guid driverId, DayOfWeek day, CancellationToken ct) =>
        await db.DriverWeeklyAvailability
            .Where(a => a.DriverId == driverId && a.DayOfWeek == day)
            .Select(a => new TimeWindow(a.StartTime, a.EndTime))
            .ToListAsync(ct);

    /// <summary>Rides and time off that overlap [from, to).</summary>
    private async Task<List<TimeSlot>> GetBusyAsync(Guid driverId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var rides = await db.Rides
            .Where(r => r.DriverId == driverId && Ride.BlockingStatuses.Contains(r.Status) && r.StartsAt < to && from < r.EndsAt)
            .Select(r => new TimeSlot(r.StartsAt, r.EndsAt))
            .ToListAsync(ct);
        var timeOff = await db.DriverTimeOff
            .Where(t => t.DriverId == driverId && t.StartsAt < to && from < t.EndsAt)
            .Select(t => new TimeSlot(t.StartsAt, t.EndsAt))
            .ToListAsync(ct);
        return [.. rides, .. timeOff];
    }
}

/// <summary>IsAvailableToday: at least one free slot left today (driver's time zone). NextAvailableAt: start of the next free slot within a week.</summary>
public record DriverAvailability(bool IsAvailableToday, DateTimeOffset? NextAvailableAt);
