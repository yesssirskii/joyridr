using Joyridr.Models;

namespace Joyridr.Common;

public static class UserNames
{
    /// <summary>"Ivan K." - riders and drivers only see each other's first name and last initial.</summary>
    public static string Display(User u) => $"{u.FirstName} {u.LastName[..1]}.";
}
