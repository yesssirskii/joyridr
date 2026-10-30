using Joyridr.Common;
using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

/// <summary>Driver weekly schedules, time off, and the bookable slots that come out of them.</summary>
[Route("api")]
[Authorize]
public class ScheduleController(ScheduleService schedule) : ApiControllerBase
{
    /// <summary>The logged-in driver's weekly schedule.</summary>
    [HttpGet("drivers/me/schedule")]
    [Authorize(Policy = Policies.Driver)]
    public Task<WeeklyScheduleDto> GetMySchedule(CancellationToken ct) => schedule.GetWeeklyScheduleAsync(CurrentUserId, ct);

    /// <summary>Replace the driver's whole weekly schedule. Times are in the driver's time zone, on a 30-minute grid.</summary>
    [HttpPut("drivers/me/schedule")]
    [Authorize(Policy = Policies.Driver)]
    public Task<WeeklyScheduleDto> ReplaceMySchedule(UpdateWeeklyScheduleRequest request, CancellationToken ct) =>
        schedule.ReplaceWeeklyScheduleAsync(CurrentUserId, request, ct);

    /// <summary>A driver's weekly schedule (in their time zone).</summary>
    [HttpGet("drivers/{driverId:guid}/schedule")]
    public Task<WeeklyScheduleDto> GetDriverSchedule(Guid driverId, CancellationToken ct) => schedule.GetWeeklyScheduleAsync(driverId, ct);

    /// <summary>The logged-in driver's upcoming time off.</summary>
    [HttpGet("drivers/me/time-off")]
    [Authorize(Policy = Policies.Driver)]
    public Task<List<TimeOffDto>> GetMyTimeOff(CancellationToken ct) => schedule.GetUpcomingTimeOffAsync(CurrentUserId, ct);

    /// <summary>Block out a period (holiday, car in service...). Fails if rides are booked in it.</summary>
    [HttpPost("drivers/me/time-off")]
    [Authorize(Policy = Policies.Driver)]
    public async Task<ActionResult<TimeOffDto>> AddTimeOff(CreateTimeOffRequest request, CancellationToken ct)
    {
        var timeOff = await schedule.AddTimeOffAsync(CurrentUserId, request, ct);
        return Created($"/api/drivers/me/time-off/{timeOff.Id}", timeOff);
    }

    /// <summary>Remove a time-off block.</summary>
    [HttpDelete("drivers/me/time-off/{timeOffId:guid}")]
    [Authorize(Policy = Policies.Driver)]
    public async Task<IActionResult> DeleteTimeOff(Guid timeOffId, CancellationToken ct)
    {
        await schedule.DeleteTimeOffAsync(CurrentUserId, timeOffId, ct);
        return NoContent();
    }

    /// <summary>Start times a car can be booked at on a given date (date is in the driver's time zone).</summary>
    [HttpGet("cars/{carId:guid}/slots")]
    public Task<SlotsResponse> GetSlots(Guid carId, [FromQuery] DateOnly date, [FromQuery] int durationMinutes, CancellationToken ct) =>
        schedule.GetSlotsAsync(carId, date, durationMinutes, ct);
}
