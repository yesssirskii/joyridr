using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

/// <summary>Blocking works both ways: neither person can message or book the other.</summary>
public class BlockService(AppDbContext db)
{
    public async Task<List<BlockedUserDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        var blocks = await db.UserBlocks
            .Where(b => b.BlockerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Include(b => b.Blocked)
            .ToListAsync(ct);
        return blocks.Select(b => new BlockedUserDto(b.BlockedId, UserNames.Display(b.Blocked), b.Blocked.AvatarUrl, b.CreatedAt)).ToList();
    }

    public async Task BlockAsync(Guid userId, Guid otherUserId, CancellationToken ct)
    {
        if (otherUserId == userId)
            throw ApiException.BadRequest("You can't block yourself.", "block_self");
        if (!await db.Users.AnyAsync(u => u.Id == otherUserId && u.DeletedAt == null, ct))
            throw ApiException.NotFound("User");
        if (await db.UserBlocks.AnyAsync(b => b.BlockerId == userId && b.BlockedId == otherUserId, ct))
            return;

        db.UserBlocks.Add(new UserBlock { BlockerId = userId, BlockedId = otherUserId });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        // Blocked twice at the same moment (e.g. a double tap): the first one counts.
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
        }
    }

    public Task UnblockAsync(Guid userId, Guid otherUserId, CancellationToken ct) =>
        db.UserBlocks.Where(b => b.BlockerId == userId && b.BlockedId == otherUserId).ExecuteDeleteAsync(ct);

    /// <summary>True if either of the two has blocked the other.</summary>
    public static Task<bool> AnyBetweenAsync(AppDbContext db, Guid a, Guid b, CancellationToken ct) =>
        db.UserBlocks.AnyAsync(x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a), ct);

    public static ApiException Blocked(string message) => new(StatusCodes.Status403Forbidden, message, "blocked");
}
