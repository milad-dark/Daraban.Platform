using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Platform.Hosting;

/// <summary>
/// Single owner of the ProblemDetails response customization for every host
/// (Daraban.Host.Api, Daraban.Host.AgentApi). Both hosts used to carry an identical
/// inline AddProblemDetails(...) block; if one drifted the two would diverge silently.
/// The exception-to-ProblemDetails mapping itself lives in GlobalExceptionHandler and
/// ErrorProblemDetailsExtensions -- this extension only wires the shared request metadata
/// (traceId + instance) onto every ProblemDetails the framework produces.
/// </summary>
public static class ProblemDetailsServiceCollectionExtensions
{
    public static IServiceCollection AddDarabanProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                context.ProblemDetails.Extensions["instance"] = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            };
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }
}