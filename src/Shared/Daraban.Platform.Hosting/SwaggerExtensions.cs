using Daraban.Platform.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared Swagger/OpenAPI setup for every host (ADR-002: one owner per
/// infrastructure concern). Registers a Bearer (JWT) security scheme so the
/// Swagger UI exposes an Authorize button; tokens are the Identity module's
/// RS256-signed JWTs from /api/v1/identity/auth/login, so plain HTTP bearer
/// is the correct scheme (no API key, no OAuth flow).
/// </summary>
public static class SwaggerExtensions
{
    public const string BearerSchemeName = "Bearer";

    public static IServiceCollection AddDarabanSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste the access token from /api/v1/identity/auth/login (or the agent client-credentials token). The 'Bearer ' prefix is added automatically.",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });

            // Swashbuckle v10 binds the requirement late, against the built document —
            // the reference must point at that document.
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = []
            });

            options.SupportNonNullableReferenceTypes();
            options.UseInlineDefinitionsForEnums();
        });

        return services;
    }

    /// <summary>Adds the Swagger middleware (and UI) — call inside
    /// <c>if (app.Environment.IsDevelopment())</c> or unconditionally per host policy.
    /// The Bearer security scheme makes the UI's Authorize button send
    /// <c>Authorization: Bearer &lt;token&gt;</c> natively — no interceptor needed.</summary>
    public static WebApplication UseDarabanSwagger(this WebApplication app, Action<SwaggerUIOptions>? configureUi = null)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.DocumentTitle = "Daraban Platform API";
            configureUi?.Invoke(options);
        });

        return app;
    }
}
