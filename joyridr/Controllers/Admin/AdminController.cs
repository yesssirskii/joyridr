using Joyridr.Common;
using Joyridr.DTOs;
using Joyridr.Models.Enums;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

/// <summary>Car review queue, driver verification, and moderation of reported users and content.</summary>
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

    /// <summary>Reports with the given status (open by default), oldest first.</summary>
    [HttpGet("reports")]
    public Task<List<ReportDto>> ListReports(CancellationToken ct, ReportStatus status = ReportStatus.Open) =>
        admin.ListReportsAsync(status, ct);

    /// <summary>Close a report without doing anything.</summary>
    [HttpPost("reports/{reportId:guid}/dismiss")]
    public async Task<IActionResult> DismissReport(Guid reportId, CancellationToken ct)
    {
        await admin.DismissReportAsync(CurrentUserId, reportId, ct);
        return NoContent();
    }

    /// <summary>Delete the reported message or review. Closes every open report about it.</summary>
    [HttpPost("reports/{reportId:guid}/remove-content")]
    public async Task<IActionResult> RemoveReportedContent(Guid reportId, CancellationToken ct)
    {
        await admin.RemoveReportedContentAsync(CurrentUserId, reportId, ct);
        return NoContent();
    }

    /// <summary>Ban a user: logged out everywhere, can't log in, upcoming rides cancelled with full refunds, cars hidden.</summary>
    [HttpPost("users/{userId:guid}/ban")]
    public async Task<IActionResult> BanUser(Guid userId, BanUserRequest request, CancellationToken ct)
    {
        await admin.BanUserAsync(CurrentUserId, userId, request.Reason, ct);
        return NoContent();
    }
}
