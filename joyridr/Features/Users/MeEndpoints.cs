using System.Security.Claims;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.Domain;
using Joyridr.Features.Drivers;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Features.Users;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/me").WithTags("Me").RequireAuthorization();

        group.MapGet("/", GetMe).WithSummary("The logged-in user with their rider or driver profile.");

        group.MapPut("/", async (UpdateMeRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == principal.GetUserId(), ct);
            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.PhoneNumber = request.PhoneNumber?.Trim();
            await db.SaveChangesAsync(ct);
            return await GetMe(principal, db, ct);
        });

        group.MapPut("/rider-profile", async (UpdateRiderProfileRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var profile = await db.RiderProfiles.FirstOrDefaultAsync(p => p.UserId == principal.GetUserId(), ct)
                          ?? throw ApiException.Forbidden("Only JoyRiders have a rider profile.");
            profile.DateOfBirth = request.DateOfBirth;
            await db.SaveChangesAsync(ct);
            return await GetMe(principal, db, ct);
        }).RequireAuthorization(Policies.Rider);

        group.MapPost("/avatar", async (IFormFile file, ClaimsPrincipal principal, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var user = await db.Users.FirstAsync(u => u.Id == principal.GetUserId(), ct);
            var oldAvatar = user.AvatarUrl;
            user.AvatarUrl = await storage.SaveAsync($"avatars/{user.Id}", file, ct);
            await db.SaveChangesAsync(ct);
            if (oldAvatar is not null)
                await storage.DeleteAsync(oldAvatar, ct);
            return UserDto.From(user);
        }).DisableAntiforgery().WithSummary("Upload a profile photo (multipart/form-data, field name \"file\").");

        return app;
    }

    private static async Task<MeResponse> GetMe(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.RiderProfile)
            .Include(u => u.DriverProfile)
            .FirstOrDefaultAsync(u => u.Id == principal.GetUserId(), ct)
            ?? throw ApiException.NotFound("User");

        return new MeResponse(
            UserDto.From(user),
            user.RiderProfile is null ? null : RiderProfileDto.From(user.RiderProfile),
            user.DriverProfile is null ? null : DriverProfileDto.From(user.DriverProfile));
    }
}
