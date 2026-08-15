using Daraban.Platform.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Platform.Hosting;

/// <summary>
/// Single place mapping Common.Error -> RFC 7807 ProblemDetails (Task 1.4 SS6), replacing
/// the private ProblemFrom() helper that had already been copy-pasted into both
/// UsersController and AuthController -- exactly the kind of duplication that's cheap to
/// fix now and annoying to fix consistently later once ten more controllers have their own
/// slightly-drifted copy.
/// </summary>
public static class ErrorProblemDetailsExtensions
{
    public static ObjectResult ToProblemResult(this Error error, HttpContext httpContext)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };

        var problem = new ProblemDetails
        {
            Title = error.Message,
            Status = status,
            Type = $"https://daraban.local/errors/{error.Type.ToString().ToLowerInvariant()}",
            Extensions =
            {
                ["errorCode"] = error.Code,
                ["traceId"] = httpContext.TraceIdentifier,
            },
        };

        return new ObjectResult(problem) { StatusCode = status };
    }
}
