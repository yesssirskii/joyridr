using Joyridr.Common;
using Joyridr.Models.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

/// <summary>Base for all API controllers: JSON in/out, automatic 400s for invalid input, and quick access to the caller.</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the logged-in user (from the access token's "sub" claim).</summary>
    protected Guid CurrentUserId => User.GetUserId();

    protected UserRole CurrentUserRole => User.GetRole();
}
