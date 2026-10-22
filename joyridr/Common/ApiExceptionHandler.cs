using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Common;

public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Minimal APIs throw this when a route/query/body value can't be parsed (e.g. "?page=abc").
        if (exception is BadHttpRequestException badRequest)
            exception = new ApiException(badRequest.StatusCode, badRequest.Message, "bad_request");

        if (exception is not ApiException apiException)
            return false;

        httpContext.Response.StatusCode = apiException.StatusCode;
        var problem = new ProblemDetails
        {
            Status = apiException.StatusCode,
            Title = apiException.Message,
        };
        if (apiException.Code is not null)
            problem.Extensions["code"] = apiException.Code;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
