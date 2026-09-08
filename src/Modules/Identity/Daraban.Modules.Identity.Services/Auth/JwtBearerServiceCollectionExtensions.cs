using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Daraban.Modules.Identity.Services.Auth;

/// <summary>
/// Single owner of the JWT bearer validation policy shared by every host
/// (Daraban.Host.Api, Daraban.Host.AgentApi). Both hosts used to carry an identical
/// inline AddAuthentication/AddOptions&lt;JwtBearerOptions&gt; block that could drift apart;
/// the Identity module owns token issuance (JwtTokenService, AgentAuthService) so it is
/// the natural owner of the matching validation setup too.
///
/// Validation parameters are fixed platform policy (same issuer/audience/signing key for
/// every token the platform issues); the only per-host knob is whether HTTPS is required,
/// which the caller derives from its environment. Host-specific behavior -- e.g. Host.Api's
/// token_version revocation check -- is layered on afterwards with an additional
/// Configure&lt;JwtBearerOptions&gt; action and runs after this one.
/// </summary>
public static class JwtBearerServiceCollectionExtensions
{
    public static IServiceCollection AddDarabanJwtBearer(
        this IServiceCollection services, IConfiguration configuration, bool requireHttpsMetadata)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSigningKeyProvider>((options, keyProvider) =>
            {
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.MapInboundClaims = false; // keep claim names exactly as issued (e.g. "token_version"), not remapped to long XML-namespace URIs
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30), // tight -- these are already short-lived (15 min) tokens, not the 5-minute default
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new RsaSecurityKey(keyProvider.GetKey()),
                };
            });
        return services;
    }
}