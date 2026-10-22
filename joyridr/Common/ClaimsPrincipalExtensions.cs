using System.Security.Claims;
using Joyridr.Models.Enums;

namespace Joyridr.Common;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id)
            ? id
            : throw ApiException.Unauthorized("Missing or invalid user id in token.");

    public static UserRole GetRole(this ClaimsPrincipal user) =>
        Enum.TryParse<UserRole>(user.FindFirstValue("role"), out var role)
            ? role
            : throw ApiException.Unauthorized("Missing or invalid role in token.");
}
