using Joyridr.DTOs;

namespace Joyridr.Services;

public record TimeWindow(TimeOnly Start, TimeOnly End);

/// <summary>
/// Pure scheduling logic (no database), so it's easy to unit test. Weekly windows are in the
/// driver's local time; everything else is UTC.
/// </summary>
public static class ScheduleCalculator
{
    /// <summary>
    /// Bookable start times on <paramref name="date"/> (a date in the driver's time zone) for a ride of
    /// <paramref name="durationMinutes"/>, every <paramref name="stepMinutes"/> from the start of each window.
    /// </summary>
    public static List<TimeSlot> GetSlots(
        DateOnly date,
        TimeZoneInfo timeZone,
        IReadOnlyCollection<TimeWindow> windowsForDay,
        IReadOnlyCollection<TimeSlot> busy,
        DateTimeOffset earliestStart,
        int durationMinutes,
        int stepMinutes)
    {
        var duration = TimeSpan.FromMinutes(durationMinutes);
        var step = TimeSpan.FromMinutes(stepMinutes);
        var slots = new List<TimeSlot>();

        foreach (var window in windowsForDay.OrderBy(w => w.Start))
        {
            // Loop over TimeSpans instead of TimeOnly so we can't wrap around midnight.
            for (var offset = window.Start.ToTimeSpan(); offset + duration <= window.End.ToTimeSpan(); offset += step)
            {
                var local = date.ToDateTime(TimeOnly.FromTimeSpan(offset));
                if (timeZone.IsInvalidTime(local))
                    continue; // e.g. 02:30 on the night clocks spring forward

                var startsAt = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, timeZone), TimeSpan.Zero);
                var slot = new TimeSlot(startsAt, startsAt + duration);

                if (slot.StartsAt < earliestStart)
                    continue;
                if (!IsWithinWindows(slot, timeZone, windowsForDay))
                    continue;
                if (busy.Any(b => slot.Overlaps(b.StartsAt, b.EndsAt)))
                    continue;
                if (slots.All(s => s.StartsAt != slot.StartsAt))
                    slots.Add(slot);
            }
        }

        return slots.OrderBy(s => s.StartsAt).ToList();
    }

    /// <summary>True if the whole slot falls inside one weekly window on the same local day.</summary>
    public static bool IsWithinWindows(TimeSlot slot, TimeZoneInfo timeZone, IEnumerable<TimeWindow> windowsForDay)
    {
        var localStart = TimeZoneInfo.ConvertTime(slot.StartsAt, timeZone);
        var localEnd = TimeZoneInfo.ConvertTime(slot.EndsAt, timeZone);
        if (DateOnly.FromDateTime(localStart.DateTime) != DateOnly.FromDateTime(localEnd.DateTime))
            return false;

        var start = TimeOnly.FromDateTime(localStart.DateTime);
        var end = TimeOnly.FromDateTime(localEnd.DateTime);
        return windowsForDay.Any(w => w.Start <= start && end <= w.End);
    }

    /// <summary>The UTC instants where the given local date begins and ends in the time zone.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) DayBoundsUtc(DateOnly date, TimeZoneInfo timeZone)
    {
        var start = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), timeZone);
        var end = TimeZoneInfo.ConvertTimeToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);
        return (new DateTimeOffset(start, TimeSpan.Zero), new DateTimeOffset(end, TimeSpan.Zero));
    }

    /// <summary>
    /// Validates a full weekly schedule: windows are on the step grid (e.g. 10:00 or 10:30, not 10:15),
    /// end after they start, and don't overlap on the same day.
    /// </summary>
    public static string? Validate(IEnumerable<(DayOfWeek Day, TimeWindow Window)> schedule, int stepMinutes)
    {
        foreach (var day in schedule.GroupBy(s => s.Day))
        {
            var windows = day.Select(d => d.Window).OrderBy(w => w.Start).ToList();
            if (windows.Any(w => w.End <= w.Start))
                return $"On {day.Key}, every window must end after it starts.";
            if (windows.Any(w => !IsOnGrid(w.Start, stepMinutes) || !IsOnGrid(w.End, stepMinutes)))
                return $"On {day.Key}, windows must start and end on a {stepMinutes}-minute boundary.";
            for (var i = 1; i < windows.Count; i++)
                if (windows[i].Start < windows[i - 1].End)
                    return $"On {day.Key}, windows overlap.";
        }
        return null;
    }

    /// <summary>
    /// The first bookable slot from <paramref name="today"/> (a date in the driver's time zone) onwards,
    /// looking at most <paramref name="days"/> days ahead. Null if the driver is fully booked or off.
    /// </summary>
    public static TimeSlot? FindNextSlot(
        DateOnly today,
        TimeZoneInfo timeZone,
        IReadOnlyDictionary<DayOfWeek, List<TimeWindow>> weeklyWindows,
        IReadOnlyCollection<TimeSlot> busy,
        DateTimeOffset earliestStart,
        int durationMinutes,
        int stepMinutes,
        int days)
    {
        for (var i = 0; i < days; i++)
        {
            var date = today.AddDays(i);
            if (!weeklyWindows.TryGetValue(date.DayOfWeek, out var windows) || windows.Count == 0)
                continue;
            var first = GetSlots(date, timeZone, windows, busy, earliestStart, durationMinutes, stepMinutes).FirstOrDefault();
            if (first is not null)
                return first;
        }
        return null;
    }

    public static bool IsOnGrid(TimeOnly time, int stepMinutes) =>
        time.Second == 0 && time.Millisecond == 0 && (time.Hour * 60 + time.Minute) % stepMinutes == 0;
}
