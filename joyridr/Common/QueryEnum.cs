using System.Text.Json;

namespace Joyridr.Common;

/// <summary>
/// Parses enums from query strings case-insensitively, so "?view=past" works just like the
/// camelCase values in JSON bodies. Minimal APIs' built-in enum binding is case-sensitive.
/// </summary>
public static class QueryEnum
{
    public static T? Parse<T>(string? value, string parameterName) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (Enum.TryParse<T>(value, ignoreCase: true, out var result) && Enum.IsDefined(result) && !int.TryParse(value, out _))
            return result;

        var allowed = string.Join(", ", Enum.GetNames<T>().Select(JsonNamingPolicy.CamelCase.ConvertName));
        throw ApiException.BadRequest($"Invalid {parameterName} '{value}'. Allowed values: {allowed}.", "invalid_query");
    }
}
