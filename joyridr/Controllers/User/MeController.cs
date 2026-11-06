using Joyridr.Common;
using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

[Route("api/me")]
[Authorize]
public class MeController(UserService users, AccountService accounts) : ApiControllerBase
{
    /// <summary>The logged-in user with their rider or driver profile.</summary>
    [HttpGet]
    public Task<MeResponse> Get(CancellationToken ct) => users.GetMeAsync(CurrentUserId, ct);

    /// <summary>Update name and phone number.</summary>
    [HttpPut]
    public Task<MeResponse> Update(UpdateMeRequest request, CancellationToken ct) => users.UpdateMeAsync(CurrentUserId, request, ct);

    /// <summary>Update rider-only details.</summary>
    [HttpPut("rider-profile")]
    [Authorize(Policy = Policies.Rider)]
    public Task<MeResponse> UpdateRiderProfile(UpdateRiderProfileRequest request, CancellationToken ct) =>
        users.UpdateRiderProfileAsync(CurrentUserId, request, ct);

    /// <summary>Upload a profile photo (multipart/form-data, field name "file").</summary>
    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public Task<UserDto> UploadAvatar(IFormFile file, CancellationToken ct) => users.UploadAvatarAsync(CurrentUserId, file, ct);

    /// <summary>
    /// Delete the account for good, confirmed with the current password. Upcoming rides are cancelled under
    /// the normal rules, personal data, messages and review text are wiped, and every device is logged out.
    /// Ride and payment records are kept (without personal data) for accounting.
    /// </summary>
    [HttpPost("delete")]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, CancellationToken ct)
    {
        await accounts.DeleteAsync(CurrentUserId, request.Password, ct);
        return NoContent();
    }
}
