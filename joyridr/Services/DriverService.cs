using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

public class DriverService(AppDbContext db)
{
    public async Task<DriverProfileDto> GetOwnProfileAsync(Guid driverId, CancellationToken ct) =>
        DriverProfileDto.From(await LoadProfileAsync(driverId, ct));

    public async Task<DriverProfileDto> UpdateProfileAsync(Guid driverId, UpdateDriverProfileRequest request, CancellationToken ct)
    {
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out _))
            throw ApiException.BadRequest($"Unknown time zone '{request.TimeZoneId}'. Use an IANA id like \"Europe/Zagreb\".", "invalid_time_zone");
        if (request.BaseLatitude is null != request.BaseLongitude is null)
            throw ApiException.BadRequest("Send both baseLatitude and baseLongitude, or neither.", "invalid_location");

        var profile = await LoadProfileAsync(driverId, ct);
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
    }

    public async Task<PublicDriverProfileDto> GetPublicProfileAsync(Guid driverId, CancellationToken ct)
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
    }

    private async Task<JoyDriverProfile> LoadProfileAsync(Guid driverId, CancellationToken ct) =>
        await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == driverId, ct)
        ?? throw ApiException.NotFound("Driver profile");
}
