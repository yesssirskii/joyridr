using System.Security.Claims;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.Domain;
using Joyridr.Features.Cars;
using Joyridr.Features.Rides;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Features.Drivers;

public static class DriverEndpoints
{
    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/drivers").WithTags("Drivers").RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
            DriverProfileDto.From(await GetOwnProfile(principal, db, ct))).RequireAuthorization(Policies.Driver);

        group.MapPut("/me", async (UpdateDriverProfileRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out _))
                throw ApiException.BadRequest($"Unknown time zone '{request.TimeZoneId}'. Use an IANA id like \"Europe/Zagreb\".", "invalid_time_zone");
            if (request.BaseLatitude is null != request.BaseLongitude is null)
                throw ApiException.BadRequest("Send both baseLatitude and baseLongitude, or neither.", "invalid_location");

            var profile = await GetOwnProfile(principal, db, ct);
            profile.Bio = request.Bio?.Trim();
            profile.City = request.City?.Trim();
            profile.BaseLatitude = request.BaseLatitude;
            profile.BaseLongitude = request.BaseLongitude;
            profile.BaseAddress = request.BaseAddress?.Trim();
            profile.ServiceRadiusKm = request.ServiceRadiusKm;
            profile.TimeZoneId = request.TimeZoneId;
            profile.IsAcceptingRides = request.IsAcceptingRides;
            await db.SaveChangesAsync(ct);
            return DriverProfileDto.From(profile);
        }).RequireAuthorization(Policies.Driver);

        group.MapGet("/me/cars", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var driverId = principal.GetUserId();
            var cars = await db.Cars
                .Include(c => c.Photos)
                .Include(c => c.Driver).ThenInclude(d => d.User)
                .Where(c => c.DriverId == driverId && c.DeletedAt == null)
                .OrderBy(c => c.CreatedAt)
                .AsSplitQuery()
                .ToListAsync(ct);
            return cars.Select(c => CarDetailDto.From(c, isOwner: true)).ToList();
        }).RequireAuthorization(Policies.Driver).WithSummary("All of the driver's cars, including ones pending review.");

        group.MapGet("/{driverId:guid}", async (Guid driverId, AppDbContext db, CancellationToken ct) =>
        {
            var profile = await db.DriverProfiles
                .Include(d => d.User)
                .Include(d => d.Cars.Where(c => c.Status == CarStatus.Approved && c.IsActive && c.DeletedAt == null))
                    .ThenInclude(c => c.Photos)
                .AsSplitQuery()
                .FirstOrDefaultAsync(d => d.UserId == driverId, ct)
                ?? throw ApiException.NotFound("Driver");

            var reviews = await db.Reviews
                .Where(r => r.SubjectId == driverId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(10)
                .Include(r => r.Author)
                .ToListAsync(ct);

            return new PublicDriverProfileDto(
                DriverSummaryDto.From(profile),
                profile.Bio,
                profile.CompletedRides,
                profile.User.CreatedAt,
                profile.Cars.Select(CarSummaryDto.From).ToList(),
                reviews.Select(ReviewDto.From).ToList());
        }).WithSummary("Public driver profile with their listed cars and recent reviews.");

        return app;
    }

    private static async Task<JoyDriverProfile> GetOwnProfile(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == principal.GetUserId(), ct)
        ?? throw ApiException.NotFound("Driver profile");
}
