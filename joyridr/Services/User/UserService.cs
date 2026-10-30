using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

public class UserService(AppDbContext db, IFileStorage storage)
{
    public async Task<MeResponse> GetMeAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.RiderProfile)
            .Include(u => u.DriverProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw ApiException.NotFound("User");

        return new MeResponse(
            UserDto.From(user),
            user.RiderProfile is null ? null : RiderProfileDto.From(user.RiderProfile),
            user.DriverProfile is null ? null : DriverProfileDto.From(user.DriverProfile));
    }

    public async Task<MeResponse> UpdateMeAsync(Guid userId, UpdateMeRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw ApiException.NotFound("User");
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = request.PhoneNumber?.Trim();
        await db.SaveChangesAsync(ct);
        return await GetMeAsync(userId, ct);
    }

    public async Task<MeResponse> UpdateRiderProfileAsync(Guid userId, UpdateRiderProfileRequest request, CancellationToken ct)
    {
        var profile = await db.RiderProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct)
                      ?? throw ApiException.Forbidden("Only JoyRiders have a rider profile.");
        profile.DateOfBirth = request.DateOfBirth;
        await db.SaveChangesAsync(ct);
        return await GetMeAsync(userId, ct);
    }

    public async Task<UserDto> UploadAvatarAsync(Guid userId, IFormFile file, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw ApiException.NotFound("User");
        var oldAvatar = user.AvatarUrl;
        user.AvatarUrl = await storage.SaveAsync($"avatars/{user.Id}", file, ct);
        await db.SaveChangesAsync(ct);
        if (oldAvatar is not null)
            await storage.DeleteAsync(oldAvatar, ct);
        return UserDto.From(user);
    }
}
