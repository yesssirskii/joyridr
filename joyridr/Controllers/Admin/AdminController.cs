using Joyridr.Common;
using Joyridr.DTOs;
using Joyridr.Models.Enums;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

/// <summary>Car review queue and driver verification.</summary>
[Route("api/admin")]
[Authorize(Policy = Policies.Admin)]
public class AdminController(AdminService admin) : ApiControllerBase
{
    /// <summary>Cars with the given status (pending review by default).</summary>
    [HttpGet("cars")]
    public Task<List<CarDetailDto>> ListCars(CancellationToken ct, CarStatus status = CarStatus.PendingReview) =>
        admin.ListCarsAsync(status, ct);

    /// <summary>Approve a car so riders can book it.</summary>
    [HttpPost("cars/{carId:guid}/approve")]
    public Task<CarDetailDto> ApproveCar(Guid carId, CancellationToken ct) => admin.ApproveCarAsync(carId, ct);

    /// <summary>Reject a car, with a reason the driver will see.</summary>
    [HttpPost("cars/{carId:guid}/reject")]
    public Task<CarDetailDto> RejectCar(Guid carId, ReasonRequest request, CancellationToken ct) =>
        admin.RejectCarAsync(carId, request.Reason, ct);

    /// <summary>Mark a driver's licence/identity as checked.</summary>
    [HttpPost("drivers/{driverId:guid}/verify")]
    public async Task<IActionResult> VerifyDriver(Guid driverId, CancellationToken ct)
    {
        await admin.VerifyDriverAsync(driverId, ct);
        return NoContent();
    }
}
