using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Modules.Identity.Services.Authorization;
using Daraban.Modules.Identity.Services.Users;
using Daraban.Platform.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Modules.Identity.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 1.1 SS1). Plain DI registration, no MediatR handler scanning.</summary>
public static class IdentityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddValidatorsFromAssembly(typeof(IdentityModuleServiceCollectionExtensions).Assembly);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();

        // ---- Auth (Task 2.3) ----------------------------------------------------------
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton<JwtSigningKeyProvider>(); // singleton: Host.Api's JwtBearer validation must resolve the SAME key instance this signs with
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        // ---- Authorization (Task 2.4) ---------------------------------------------------
        services.AddScoped<IEntityScopeAccessor, EntityScopeAccessor>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();

        // TODO: register remaining Identity resources (Groups) as they're built out, same shape as above.

        return services;
    }
}
