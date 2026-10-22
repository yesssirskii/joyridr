using Joyridr.Common;
using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

[Route("api/rides")]
[Authorize]
public class RidesController(RideService rides) : ApiControllerBase
{
    /// <summary>Book a ride. Pick startsAt from the car's slots endpoint. The payment is held until the driver confirms.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Rider)]
    public async Task<ActionResult<RideDto>> Create(CreateRideRequest request, CancellationToken ct)
    {
        var ride = await rides.CreateAsync(CurrentUserId, request, ct);
        return CreatedAtAction(nameof(Get), new { rideId = ride.Id }, ride);
    }

    /// <summary>The current user's rides: as rider for JoyRiders, as driver for JoyDrivers.</summary>
    [HttpGet]
    public Task<PagedResult<RideDto>> List(CancellationToken ct, RideListView view = RideListView.Upcoming, int page = 1, int pageSize = 20) =>
        rides.ListAsync(CurrentUserId, CurrentUserRole, view, page, pageSize, ct);

    /// <summary>One ride (only visible to its rider and driver).</summary>
    [HttpGet("{rideId:guid}")]
    public Task<RideDto> Get(Guid rideId, CancellationToken ct) => rides.GetAsync(CurrentUserId, rideId, ct);

    /// <summary>Driver accepts a ride request. The rider's payment is captured.</summary>
    [HttpPost("{rideId:guid}/confirm")]
    [Authorize(Policy = Policies.Driver)]
    public Task<RideDto> Confirm(Guid rideId, CancellationToken ct) => rides.ConfirmAsync(CurrentUserId, rideId, ct);

    /// <summary>Driver turns down a ride request. The hold on the rider's card is released.</summary>
    [HttpPost("{rideId:guid}/decline")]
    [Authorize(Policy = Policies.Driver)]
    public Task<RideDto> Decline(Guid rideId, ReasonRequest request, CancellationToken ct) =>
        rides.DeclineAsync(CurrentUserId, rideId, request.Reason, ct);

    /// <summary>Rider: full refund if cancelled 24h+ before a confirmed ride, otherwise no refund. Driver: rider always gets a full refund.</summary>
    [HttpPost("{rideId:guid}/cancel")]
    public Task<RideDto> Cancel(Guid rideId, ReasonRequest request, CancellationToken ct) =>
        rides.CancelAsync(CurrentUserId, rideId, request.Reason, ct);

    /// <summary>Change where to meet. AgreedLocation: either side. DriverComesToRider: rider only, within the driver's radius.</summary>
    [HttpPut("{rideId:guid}/meeting-point")]
    public Task<RideDto> UpdateMeetingPoint(Guid rideId, UpdateMeetingPointRequest request, CancellationToken ct) =>
        rides.UpdateMeetingPointAsync(CurrentUserId, rideId, request, ct);

    /// <summary>Driver starts the ride (from 30 minutes before the start time).</summary>
    [HttpPost("{rideId:guid}/start")]
    [Authorize(Policy = Policies.Driver)]
    public Task<RideDto> Start(Guid rideId, CancellationToken ct) => rides.StartAsync(CurrentUserId, rideId, ct);

    /// <summary>Driver finishes the ride.</summary>
    [HttpPost("{rideId:guid}/complete")]
    [Authorize(Policy = Policies.Driver)]
    public Task<RideDto> Complete(Guid rideId, CancellationToken ct) => rides.CompleteAsync(CurrentUserId, rideId, ct);

    /// <summary>Rate the other person after a completed ride (once each).</summary>
    [HttpPost("{rideId:guid}/reviews")]
    public async Task<ActionResult<ReviewDto>> AddReview(Guid rideId, CreateReviewRequest request, CancellationToken ct)
    {
        var review = await rides.AddReviewAsync(CurrentUserId, rideId, request, ct);
        return CreatedAtAction(nameof(Get), new { rideId }, review);
    }
}
