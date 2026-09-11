using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Daraban.Platform.Plugins;

/// <summary>
/// Contract every Daraban plugin assembly must implement exactly once. The host locates
/// the implementing type via the manifest's <see cref="PluginManifest.EntryPoint"/>
/// (an <c>IPlugin</c> subclass name) and drives the plugin through its lifecycle:
/// ConfigureServices -> ConfigureApp -> GetMenuItems, and the reverse on unload.
/// </summary>
/// <remarks>
/// Plugin assemblies run inside an isolated, collectible <see cref="PluginLoadContext"/>
/// and must talk to the database exclusively through <see cref="IPluginDbContext"/> --
/// they never receive the host's <c>DbContext</c> or connection string.
/// </remarks>
public interface IPlugin
{
    /// <summary>Stable, unique, machine-friendly identity. Must match the manifest's id.</summary>
    string Id { get; }

    /// <summary>Human-readable display name shown in the plugin manager UI.</summary>
    string Name { get; }

    /// <summary>Semantic version of the plugin. Must match the manifest's version.</summary>
    string Version { get; }

    /// <summary>
    /// Registers the plugin's own services into a dedicated, host-filtered collection.
    /// Called once at startup or immediately after installation.
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Attaches the plugin's middleware or endpoint groups to the app pipeline.
    /// Called after ConfigureServices, while the plugin is becoming active.
    /// </summary>
    void ConfigureApp(IApplicationBuilder app);

    /// <summary>Menu entries the plugin contributes to the Angular navigation shell.</summary>
    IEnumerable<PluginMenuItem> GetMenuItems();
}
