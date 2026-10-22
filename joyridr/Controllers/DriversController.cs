using Joyridr.Common;
using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

[Route("api/drivers")]
[Authorize]
public class DriversController(DriverService drivers, CarService cars) : ApiControllerBase
{
    /// <summary>The logged-in driver's full profile (including their private base address).</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.Driver)]
    public Task<DriverProfileDto> GetMe(CancellationToken ct) => drivers.GetOwnProfileAsync(CurrentUserId, ct);

    /// <summary>Update bio, base location, pickup radius, time zone and whether new rides are accepted.</summary>
    [HttpPut("me")]
    [Authorize(Policy = Policies.Driver)]
    public Task<DriverProfileDto> UpdateMe(UpdateDriverProfileRequest request, CancellationToken ct) =>
        drivers.UpdateProfileAsync(CurrentUserId, request, ct);

    /// <summary>All of the driver's cars, including ones pending review.</summary>
    [HttpGet("me/cars")]
    [Authorize(Policy = Policies.Driver)]
    public Task<List<CarDetailDto>> GetMyCars(CancellationToken ct) => cars.GetDriverCarsAsync(CurrentUserId, ct);

    /// <summary>Public driver profile with their listed cars and recent reviews.</summary>
    [HttpGet("{driverId:guid}")]
    public Task<PublicDriverProfileDto> GetPublicProfile(Guid driverId, CancellationToken ct) =>
        drivers.GetPublicProfileAsync(driverId, ct);
}
