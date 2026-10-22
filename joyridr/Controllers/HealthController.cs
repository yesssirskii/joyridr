using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

[Route("health")]
public class HealthController : ApiControllerBase
{
    /// <summary>Liveness check.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
