using Daraban.Modules.Settings.Data;
using Daraban.Modules.Settings.Data.Repositories;
using Daraban.Modules.Settings.Services;
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

        // The settings cache uses the same IDistributedCache the platform already registers
        // (AddDistributedMemoryCache today, AddStackExchangeRedisCache when Redis lands);
        // swapping the backend stays a host-level, one-line change.
        services.AddDistributedMemoryCache();

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
