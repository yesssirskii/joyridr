namespace Joyridr.Common;

/// <summary>
/// Throw from services to return an error to the client. <see cref="ApiExceptionHandler"/>
/// turns it into a ProblemDetails response with the given status code.
/// </summary>
public class ApiException(int statusCode, string message, string? code = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    /// <summary>Stable, machine-readable error code the app can switch on, e.g. "slot_unavailable".</summary>
    public string? Code { get; } = code;

    public static ApiException BadRequest(string message, string? code = null) => new(StatusCodes.Status400BadRequest, message, code);
    public static ApiException Unauthorized(string message, string? code = null) => new(StatusCodes.Status401Unauthorized, message, code);
    public static ApiException Forbidden(string message = "You are not allowed to do this.") => new(StatusCodes.Status403Forbidden, message, "forbidden");
    public static ApiException NotFound(string what) => new(StatusCodes.Status404NotFound, $"{what} was not found.", "not_found");
    public static ApiException Conflict(string message, string? code = null) => new(StatusCodes.Status409Conflict, message, code);
}
