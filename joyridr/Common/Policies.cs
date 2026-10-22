using Joyridr.Domain;

namespace Joyridr.Common;

/// <summary>Authorization policy names, one per role.</summary>
public static class Policies
{
    public const string Rider = nameof(UserRole.JoyRider);
    public const string Driver = nameof(UserRole.JoyDriver);
    public const string Admin = nameof(UserRole.Admin);
}
