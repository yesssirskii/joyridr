using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

/// <summary>Blocking other users. A block works both ways: no messages and no bookings between the two.</summary>
[Route("api/blocks")]
[Authorize]
public class BlocksController(BlockService blocks) : ApiControllerBase
{
    /// <summary>People you've blocked, most recent first.</summary>
    [HttpGet]
    public Task<List<BlockedUserDto>> List(CancellationToken ct) => blocks.ListAsync(CurrentUserId, ct);

    /// <summary>Block a user. Blocking someone already blocked does nothing.</summary>
    [HttpPost]
    public async Task<IActionResult> Block(BlockUserRequest request, CancellationToken ct)
    {
        await blocks.BlockAsync(CurrentUserId, request.UserId, ct);
        return NoContent();
    }

    /// <summary>Unblock a user.</summary>
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Unblock(Guid userId, CancellationToken ct)
    {
        await blocks.UnblockAsync(CurrentUserId, userId, ct);
        return NoContent();
    }
}
