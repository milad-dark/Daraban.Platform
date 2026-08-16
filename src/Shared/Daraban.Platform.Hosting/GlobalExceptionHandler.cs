using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Daraban.Platform.Hosting;

/// <summary>
/// ASP.NET Core's IExceptionHandler pipeline (built-in since .NET 8 -- no extra package)
/// catches anything that reaches it unhandled: bugs, not the expected Result/Error failures
/// every Service method already returns deliberately (Task 1.1). Registered via
/// AddExceptionHandler&lt;GlobalExceptionHandler&gt;() + app.UseExceptionHandler() in each host.
///
/// SECURITY (Task 1.4 SS6): stack traces, exception messages, and any other internal detail
/// never reach the response body, in any environment -- only a generic title, the trace id,
/// and a fixed errorCode. Full details always go to the structured log, correlated by the
/// same trace id, which is where an operator should be looking anyway.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception,
            "Unhandled exception on {Method} {Path} (traceId {TraceId})",
            httpContext.Request.Method, httpContext.Request.Path, httpContext.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://daraban.local/errors/unexpected",
            Extensions =
            {
                ["errorCode"] = "SYSTEM.UNEXPECTED_ERROR",
                ["traceId"] = httpContext.TraceIdentifier,
            },
        };

        // Development-only convenience: the exception type/message (never the full stack
        // trace) to speed up local debugging. Still never shown outside Development.
        if (_environment.IsDevelopment())
            problem.Extensions["debugMessage"] = $"{exception.GetType().Name}: {exception.Message}";

        httpContext.Response.StatusCode = problem.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
