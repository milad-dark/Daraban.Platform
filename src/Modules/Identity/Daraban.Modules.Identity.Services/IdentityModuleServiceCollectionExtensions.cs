using Daraban.Modules.Identity.Data;
using Daraban.Modules.Identity.Data.Auditing;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Agents;
using Daraban.Modules.Identity.Services.Audit;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Modules.Identity.Services.Authorization;
using Daraban.Modules.Identity.Services.Users;
using Daraban.Platform.Abstractions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Identity.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (Task 1.1 SS1). Plain DI registration, no MediatR handler scanning.</summary>
public static class IdentityModuleServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Audit trail (Task 7.3) --------------------------------------------------
        // The interceptor is registered per-scope so its request context (actor, IP,
        // user-agent) comes from the current request. Registered here -- the module
        // composition root (ADR-005) -- so both Host.Api and Host.AgentApi get auditing
        // without per-host wiring; ADR-002's single-owner rule keeps the audit policy in
        // exactly one place.
        services.AddHttpContextAccessor();
        services.AddScoped<AuditLogSaveChangesInterceptor>();

        services.AddDbContext<IdentityDbContext>((sp, options) =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres"))
                .AddInterceptors(sp.GetRequiredService<AuditLogSaveChangesInterceptor>()));

        services.AddValidatorsFromAssembly(typeof(IdentityModuleServiceCollectionExtensions).Assembly);

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();

        // ---- Audit log reads (Task 7.3) ----------------------------------------------
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();

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

        // ---- Agent services (Task 4.1/4.4: agent auth, fleet management, remote commands) ----
        // Registered here, not in each host's Program.cs, so every host that loads the
        // Identity module (Host.Api and Host.AgentApi both) gets a working agent service
        // graph. Host.Api's AdminAgentController depends on IAgentService/IAgentCommandRepository;
        // previously they were only registered in AgentApi and Host.Api's agent endpoints
        // failed to resolve them at runtime.
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IAgentService, AgentService>();
        services.AddScoped<IAgentAuthService, AgentAuthService>();
        services.AddScoped<IAgentCommandRepository, AgentCommandRepository>();
        services.AddScoped<IAgentCommandService, AgentCommandService>();

        // TODO: register remaining Identity resources (Groups) as they're built out, same shape as above.

        return services;
    }
}
