using Daraban.Modules.Settings.Data;
using Daraban.Modules.Settings.Data.Repositories;
using Daraban.Modules.Settings.Services;
using Daraban.Platform.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Settings.Services;

/// <summary>Composition root entry point for this module -- called once from each Host's
/// Program.cs (ADR-005). Plain DI registration, no handler scanning.</summary>
public static class SettingsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddSettingsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SettingsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        // The settings cache uses the host's IDistributedCache (Task 8.2: Redis in every
                // deployed environment, in-process memory locally). Registering it here too makes the
                // module self-sufficient for its own integration tests; AddDarabanDistributedCache is
                // idempotent, so whichever registration runs first wins and the host's configuration
                // is never overridden by this call.
                services.AddDarabanDistributedCache(configuration);

        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddSingleton<SystemSettingCache>();
        services.AddScoped<IConnectivityTester, ConnectivityTester>();
        services.AddScoped<ISettingsService, SettingsService>();

        return services;
    }

    /// <summary>
    /// Runs <see cref="SettingsSeeder.SeedAsync"/> once at host startup, ensuring the
    /// core.system_settings table exists and matches the catalog before the first request.
    /// Call from Program.cs as <c>await app.Services.UseSettingsSeederAsync();</c> after
    /// <c>var app = builder.Build()</c> and before <c>app.Run()</c>. Takes IServiceProvider
    /// (not WebApplication) so this module stays free of the ASP.NET Core framework
    /// reference and remains usable from workers.
    /// </summary>
    public static async Task UseSettingsSeederAsync(this IServiceProvider rootServices, CancellationToken ct = default)
    {
        var logger = rootServices.GetRequiredService<ILogger<SettingsSeeder>>();
        var configuration = rootServices.GetRequiredService<IConfiguration>();

        var scope = rootServices.CreateScope();
        using (scope)
        {
            var repository = scope.ServiceProvider.GetRequiredService<ISystemSettingRepository>();
            var seeder = new SettingsSeeder(
                repository,
                configuration.GetConnectionString("Postgres") ?? string.Empty,
                logger);
            await seeder.SeedAsync(ct);
        }
    }
}
