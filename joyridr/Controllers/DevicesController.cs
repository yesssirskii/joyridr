using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

/// <summary>APNs device tokens for push notifications.</summary>
[Route("api/devices")]
[Authorize]
public class DevicesController(NotificationService notifications) : ApiControllerBase
{
    /// <summary>Register the APNs device token after the user allows notifications. Call on every app launch.</summary>
    [HttpPost]
    public async Task<IActionResult> Register(RegisterDeviceRequest request, CancellationToken ct)
    {
        await notifications.RegisterDeviceAsync(CurrentUserId, request.Token, ct);
        return NoContent();
    }

    /// <summary>Unregister on logout.</summary>
    [HttpDelete("{token}")]
    public async Task<IActionResult> Unregister(string token, CancellationToken ct)
    {
        await notifications.UnregisterDeviceAsync(CurrentUserId, token, ct);
        return NoContent();
    }
}
