using Daraban.Modules.Plugins.Data;
using Daraban.Modules.Plugins.Data.Repositories;
using Daraban.Modules.Plugins.Services;
using Daraban.Platform.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daraban.Modules.Plugins.Services;

/// <summary>Composition root entry point for this module -- called once from each
/// Host's Program.cs (ADR-005). Plain DI registration, no handler scanning.</summary>
public static class PluginsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddPluginsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PluginsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddOptions<PluginsOptions>()
            .Bind(configuration.GetSection(PluginsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IPluginRepository, PluginRepository>();
        services.AddSingleton<IPluginManager, PluginManager>();
        services.AddScoped<IPluginService, PluginService>();

        return services;
    }

    /// <summary>
    /// Runs <see cref="PluginsSeeder.SeedAsync"/> once at host startup, ensuring the
    /// core.plugins table exists before the first request. Call from Program.cs as
    /// <c>await app.Services.UsePluginsSeederAsync();</c> after <c>var app = builder.Build()</c>.
    /// Takes IServiceProvider (not WebApplication) so this module stays free of the
    /// ASP.NET Core framework reference and remains usable from workers.
    /// </summary>
    public static async Task UsePluginsSeederAsync(this IServiceProvider rootServices, CancellationToken ct = default)
    {
        var logger = rootServices.GetRequiredService<ILogger<PluginsSeeder>>();
        var configuration = rootServices.GetRequiredService<IConfiguration>();

        var scope = rootServices.CreateScope();
        using (scope)
        {
            var repository = scope.ServiceProvider.GetRequiredService<IPluginRepository>();
            var seeder = new PluginsSeeder(
                configuration.GetConnectionString("Postgres") ?? string.Empty,
                logger);
            await seeder.SeedAsync(ct);
        }

        // Re-load every plugin the registry says is enabled, so a host restart
        // restores the pre-restart runtime state (menu items, plugin services).
        // PluginManager is a singleton, so it is safe to resolve from the root
        // provider; IPluginRepository is scoped (owns a DbContext), so it is
        // resolved inside a fresh scope. Failures are logged and skipped --
        // one broken plugin must not prevent the host from starting.
        var manager = rootServices.GetRequiredService<IPluginManager>();

        using (var loadScope = rootServices.CreateScope())
        {
            var repository = loadScope.ServiceProvider.GetRequiredService<IPluginRepository>();
            var plugins = await repository.GetActiveAsync(ct);

            foreach (var plugin in plugins)
            {
                if (plugin.Status != PluginManager.EnabledStatus)
                    continue;

                var outcome = await manager.InstallFromDirectoryAsync(plugin.PluginId, ct);
                if (!outcome.Success)
                {
                    logger.LogError(
                        "Plugin {PluginId} failed to load at startup: {Errors}",
                        plugin.PluginId,
                        string.Join("; ", outcome.Errors));
                }
            }
        }
    }
}