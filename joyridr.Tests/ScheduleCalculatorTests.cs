using Joyridr.DTOs;
using Joyridr.Services;

namespace Joyridr.Tests;

public class ScheduleCalculatorTests
{
    private static readonly TimeZoneInfo Zagreb = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zagreb");
    private static readonly DateOnly SummerDay = new(2026, 7, 15); // Zagreb is UTC+2
    private static readonly DateTimeOffset LongAgo = DateTimeOffset.MinValue;

    private static TimeWindow Window(int startHour, int startMinute, int endHour, int endMinute) =>
        new(new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

    private static DateTimeOffset Utc(int hour, int minute) => new(2026, 7, 15, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void Generates_slots_every_step_that_fit_in_the_window()
    {
        var slots = ScheduleCalculator.GetSlots(SummerDay, Zagreb, [Window(10, 0, 12, 0)], [], LongAgo, durationMinutes: 60, stepMinutes: 30);

        // 10:00, 10:30, 11:00 local = 08:00, 08:30, 09:00 UTC. 11:30 would end after the window.
        Assert.Equal([Utc(8, 0), Utc(8, 30), Utc(9, 0)], slots.Select(s => s.StartsAt));
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromHours(1), s.EndsAt - s.StartsAt));
    }

    [Fact]
    public void Skips_slots_that_overlap_busy_time_but_allows_back_to_back()
    {
        var busy = new TimeSlot(Utc(9, 0), Utc(9, 30)); // 11:00-11:30 local

        var slots = ScheduleCalculator.GetSlots(SummerDay, Zagreb, [Window(10, 0, 12, 0)], [busy], LongAgo, 60, 30);

        // 10:00-11:00 ends exactly when the busy block starts, so it's fine.
        Assert.Equal([Utc(8, 0)], slots.Select(s => s.StartsAt));
    }

    [Fact]
    public void Skips_slots_before_the_earliest_start()
    {
        var slots = ScheduleCalculator.GetSlots(SummerDay, Zagreb, [Window(10, 0, 12, 0)], [], earliestStart: Utc(8, 15), 60, 30);

        Assert.Equal([Utc(8, 30), Utc(9, 0)], slots.Select(s => s.StartsAt));
    }

    [Fact]
    public void Handles_multiple_windows_on_one_day()
    {
        var slots = ScheduleCalculator.GetSlots(SummerDay, Zagreb, [Window(18, 0, 19, 0), Window(9, 0, 10, 0)], [], LongAgo, 60, 30);

        Assert.Equal([Utc(7, 0), Utc(16, 0)], slots.Select(s => s.StartsAt));
    }

    [Fact]
    public void Skips_local_times_that_do_not_exist_when_clocks_spring_forward()
    {
        // On 29 March 2026 Zagreb jumps from 02:00 straight to 03:00.
        var slots = ScheduleCalculator.GetSlots(new DateOnly(2026, 3, 29), Zagreb, [Window(1, 0, 4, 0)], [], LongAgo, 30, 30);

        var localStarts = slots.Select(s => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(s.StartsAt, Zagreb).DateTime));
        Assert.Equal([new TimeOnly(1, 0), new TimeOnly(1, 30), new TimeOnly(3, 0), new TimeOnly(3, 30)], localStarts);
    }

    [Fact]
    public void IsWithinWindows_requires_the_whole_ride_inside_one_window()
    {
        TimeWindow[] windows = [Window(10, 0, 12, 0), Window(12, 0, 14, 0)];

        Assert.True(ScheduleCalculator.IsWithinWindows(new TimeSlot(Utc(8, 0), Utc(10, 0)), Zagreb, windows));
        // 11:00-13:00 local spans two windows; we don't stitch them together.
        Assert.False(ScheduleCalculator.IsWithinWindows(new TimeSlot(Utc(9, 0), Utc(11, 0)), Zagreb, windows));
    }

    [Fact]
    public void Validate_accepts_a_good_schedule()
    {
        var error = ScheduleCalculator.Validate(
            [(DayOfWeek.Monday, Window(10, 0, 12, 0)), (DayOfWeek.Monday, Window(14, 0, 18, 30)), (DayOfWeek.Tuesday, Window(9, 0, 17, 0))], 30);

        Assert.Null(error);
    }

    [Theory]
    [InlineData(10, 0, 12, 0, 11, 0, 13, 0)]   // overlapping
    [InlineData(10, 0, 12, 0, 12, 15, 13, 0)]  // off the 30-minute grid
    [InlineData(10, 0, 12, 0, 15, 0, 14, 0)]   // ends before it starts
    public void Validate_rejects_bad_schedules(int h1, int m1, int h2, int m2, int h3, int m3, int h4, int m4)
    {
        var error = ScheduleCalculator.Validate(
            [(DayOfWeek.Friday, Window(h1, m1, h2, m2)), (DayOfWeek.Friday, Window(h3, m3, h4, m4))], 30);

        Assert.NotNull(error);
    }
}
