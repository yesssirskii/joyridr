using System.Text.RegularExpressions;
using Joyridr.Models;

namespace Joyridr.Common;

public static partial class UserNames
{
    public const int UsernameMaxLength = 20;

    /// <summary>"Ivan K." - riders and drivers only see each other's first name and last initial.</summary>
    public static string Display(User u) => u.DeletedAt is null ? $"{u.FirstName} {u.LastName[..1]}." : "Deleted user";

    /// <summary>
    /// Usernames are 3-20 lower-case letters, digits, dots and underscores, so "@Ivan.Kovac" and "ivan.kovac"
    /// are the same username. Returns the stored form, or throws a 400 the app can show as it is.
    /// </summary>
    public static string NormalizeUsername(string username)
    {
        var normalized = username.Trim().TrimStart('@').ToLowerInvariant();
        if (!UsernamePattern().IsMatch(normalized))
            throw ApiException.BadRequest(
                "Usernames are 3-20 characters: letters, numbers, dots and underscores.", "invalid_username");
        return normalized;
    }

    [GeneratedRegex("^[a-z0-9._]{3,20}$")]
    private static partial Regex UsernamePattern();
}
