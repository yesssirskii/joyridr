using System.ComponentModel.DataAnnotations;

namespace Joyridr.DTOs;

/// <summary>A bookable (or busy) period, always in UTC.</summary>
public record TimeSlot(DateTimeOffset StartsAt, DateTimeOffset EndsAt)
{
    public bool Overlaps(DateTimeOffset start, DateTimeOffset end) => StartsAt < end && start < EndsAt;
}

public record WeeklyWindowDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>Weekly windows are in <see cref="TimeZoneId"/> local time.</summary>
public record WeeklyScheduleDto(string TimeZoneId, IReadOnlyList<WeeklyWindowDto> Windows);

public record UpdateWeeklyScheduleRequest([Required, MaxLength(50)] List<WeeklyWindowDto> Windows);

public record TimeOffDto(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string? Reason);

public record CreateTimeOffRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, [MaxLength(200)] string? Reason);

public record SlotsResponse(Guid CarId, DateOnly Date, string TimeZoneId, int DurationMinutes, decimal Price, string Currency, IReadOnlyList<TimeSlot> Slots);
