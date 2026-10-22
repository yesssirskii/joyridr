using System.Security.Claims;
using Joyridr.Common;

namespace Joyridr.Features.Rides;

public static class RideEndpoints
{
    public static IEndpointRouteBuilder MapRideEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rides").WithTags("Rides").RequireAuthorization();

        group.MapPost("/", async (CreateRideRequest request, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
            {
                var ride = await rides.CreateAsync(principal.GetUserId(), request, ct);
                return Results.Created($"/api/rides/{ride.Id}", ride);
            })
            .RequireAuthorization(Policies.Rider)
            .WithSummary("Book a ride. Pick startsAt from the car's slots endpoint. The payment is held until the driver confirms.");

        group.MapGet("/", (ClaimsPrincipal principal, RideService rides, CancellationToken ct, string? view, int page = 1, int pageSize = 20) =>
                rides.ListAsync(principal.GetUserId(), principal.GetRole(),
                    QueryEnum.Parse<RideListView>(view, nameof(view)) ?? RideListView.Upcoming, page, pageSize, ct))
            .WithSummary("The current user's rides: as rider for JoyRiders, as driver for JoyDrivers.");

        group.MapGet("/{rideId:guid}", (Guid rideId, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
            rides.GetAsync(principal.GetUserId(), rideId, ct));

        group.MapPost("/{rideId:guid}/confirm", (Guid rideId, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
            rides.ConfirmAsync(principal.GetUserId(), rideId, ct)).RequireAuthorization(Policies.Driver);

        group.MapPost("/{rideId:guid}/decline", (Guid rideId, ReasonRequest request, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
            rides.DeclineAsync(principal.GetUserId(), rideId, request.Reason, ct)).RequireAuthorization(Policies.Driver);

        group.MapPost("/{rideId:guid}/cancel", (Guid rideId, ReasonRequest request, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
                rides.CancelAsync(principal.GetUserId(), rideId, request.Reason, ct))
            .WithSummary("Rider: full refund if cancelled 24h+ before a confirmed ride, otherwise no refund. Driver: rider always gets a full refund.");

        group.MapPut("/{rideId:guid}/meeting-point", (Guid rideId, UpdateMeetingPointRequest request, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
                rides.UpdateMeetingPointAsync(principal.GetUserId(), rideId, request, ct))
            .WithSummary("Change where to meet. AgreedLocation: either side. DriverComesToRider: rider only, within the driver's radius.");

        group.MapPost("/{rideId:guid}/start", (Guid rideId, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
            rides.StartAsync(principal.GetUserId(), rideId, ct)).RequireAuthorization(Policies.Driver);

        group.MapPost("/{rideId:guid}/complete", (Guid rideId, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
            rides.CompleteAsync(principal.GetUserId(), rideId, ct)).RequireAuthorization(Policies.Driver);

        group.MapPost("/{rideId:guid}/reviews", async (Guid rideId, CreateReviewRequest request, ClaimsPrincipal principal, RideService rides, CancellationToken ct) =>
            {
                var review = await rides.AddReviewAsync(principal.GetUserId(), rideId, request, ct);
                return Results.Created($"/api/rides/{rideId}", review);
            })
            .WithSummary("Rate the other person after a completed ride (once each).");

        return app;
    }
}
