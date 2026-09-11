using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Daraban.Modules.Plugins.Data;
using Daraban.Modules.Plugins.Services.Runtime;
using Daraban.Platform.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Daraban.Modules.Plugins.Services;

/// <summary>
/// Runtime surface of <see cref="PluginManager"/> consumed by <see cref="PluginService"/>
/// and the startup reloader. Declared as an interface (like every collaborator in this
/// module) so the service layer stays unit-testable without a live ALC or database.
/// </summary>
public interface IPluginManager
{
    /// <inheritdoc cref="PluginManager.InstallAsync"/>
    Task<PluginManager.InstallOutcome> InstallAsync(Stream package, CancellationToken ct = default);

    /// <inheritdoc cref="PluginManager.InstallFromDirectoryAsync"/>
    Task<PluginManager.InstallOutcome> InstallFromDirectoryAsync(string pluginId, CancellationToken ct = default);

    /// <inheritdoc cref="PluginManager.DisableAsync"/>
    Task DisableAsync(string pluginId);

    /// <inheritdoc cref="PluginManager.UninstallAsync"/>
    Task UninstallAsync(string pluginId, CancellationToken ct = default);

    /// <inheritdoc cref="PluginManager.MenuItems"/>
    IReadOnlyList<PluginMenuItem> MenuItems { get; }

    /// <inheritdoc cref="PluginManager.LoadedPluginIds"/>
    IReadOnlyList<string> LoadedPluginIds { get; }

    /// <inheritdoc cref="PluginManager.IsEnabled"/>
    bool IsEnabled(string pluginId);
}

/// <summary>
/// The runtime heart of the plugin framework (Task 7.5): owns every loaded plugin's
/// PluginLoadContext, restricted service provider, and lifecycle state. Install/Enable/
/// Disable/Uninstall all funnel through here; the DB registry records what this manager
/// holds in memory. The manager itself is a singleton and is the only component that
/// touches plugin types.
/// </summary>
/// <remarks>
/// Isolation guarantees:
/// - each plugin loads into its own collectible PluginLoadContext -- unloading reverses
///   the load order and the ALC is verified collectible;
/// - plugin services register into their own ServiceCollection, built into a separate
///   ServiceProvider -- the host container is never handed to plugin code;
/// - all plugin DB access goes through IPluginDbContext bound to the plugin's private
///   plugins_{id} schema (search_path confined, no cross-schema access).
/// </remarks>
public class PluginManager(
    IOptions<PluginsOptions> options,
    IConfiguration configuration,
    ILoggerFactory loggerFactory,
    ILogger<PluginManager> logger) : IPluginManager
{
    /// <summary>Registry status strings, shared with the API/service layer.</summary>
    public const string EnabledStatus = "enabled";
    public const string DisabledStatus = "disabled";
    public const string UninstalledStatus = "uninstalled";

    private readonly ConcurrentDictionary<string, LoadedPlugin> _loaded = new(StringComparer.Ordinal);

    /// <summary>A plugin currently held by this manager.</summary>
    public sealed record LoadedPlugin(
        IPlugin Plugin,
        PluginLoadContext Context,
        ServiceProvider ServiceProvider,
        IReadOnlyList<PluginMenuItem> MenuItems);

    /// <summary>Menu items from every enabled plugin, merged for the shell.</summary>
    public IReadOnlyList<PluginMenuItem> MenuItems =>
        [.. _loaded.Values.SelectMany(l => l.MenuItems)
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Ids of the plugins currently loaded (diagnostics).</summary>
    public IReadOnlyList<string> LoadedPluginIds => [.. _loaded.Keys.Order()];

    /// <summary>True when the plugin id is currently loaded and enabled.</summary>
    public bool IsEnabled(string pluginId) => _loaded.ContainsKey(pluginId);

    /// <summary>
    /// Installs a plugin package end-to-end: extract -> validate assembly -> load into
    /// ALC -> run migrations -> register services -> publish menu items. Any failure
    /// after extraction cleans up fully (directory, ALC, services), leaving no partial
    /// state. The caller (PluginService) records the registry row on success.
    /// </summary>
    public async Task<InstallOutcome> InstallAsync(Stream package, CancellationToken ct = default)
    {
        var extraction = await ExtractAndValidateAsync(package, ct);
        if (!extraction.Success)
            return InstallOutcome.Failed(extraction.Errors);

        return await LoadAndRegisterAsync(extraction.Manifest!, extraction.ExtractPath!, ct);
    }

    /// <summary>
    /// Re-enables a disabled plugin from the package kept on disk (no zip upload). The
    /// directory is the one InstallAsync extracted to; migrations re-run idempotently.
    /// </summary>
    public async Task<InstallOutcome> InstallFromDirectoryAsync(string pluginId, CancellationToken ct = default)
    {
        var extractPath = Path.Combine(options.Value.RootDirectory, pluginId);
        var manifestPath = Path.Combine(extractPath, "manifest.json");
        if (!File.Exists(manifestPath))
            return InstallOutcome.Failed([$"Plugin directory for '{pluginId}' is missing on disk."]);

        PluginManifest? manifest;
        try
        {
            await using var stream = File.OpenRead(manifestPath);
            // manifest.json is written camelCase, same options as package extraction.
            manifest = await JsonSerializer.DeserializeAsync<PluginManifest>(stream, PluginPackageManager.ManifestJsonOptions);
        }
        catch (JsonException)
        {
            return InstallOutcome.Failed([$"The stored manifest for '{pluginId}' is not valid JSON."]);
        }

        if (manifest is null || !string.Equals(manifest.Id, pluginId, StringComparison.Ordinal))
            return InstallOutcome.Failed([$"The stored manifest for '{pluginId}' is invalid."]);

        var validator = new PluginAssemblyValidator(options, loggerFactory.CreateLogger<PluginAssemblyValidator>());
        var errors = validator.Validate(Path.Combine(extractPath, manifest.AssemblyFile));
        if (errors.Count > 0)
            return InstallOutcome.Failed(errors);

        // A failed reload must NOT delete the on-disk package: it is the only copy and
        // the plugin row stays "disabled", so keepDirectoryOnFailure guards it.
        return await LoadAndRegisterAsync(manifest, extractPath, ct, keepDirectoryOnFailure: true);
    }

    private async Task<(bool Success, PluginManifest? Manifest, string? ExtractPath, List<string> Errors)> ExtractAndValidateAsync(Stream package, CancellationToken ct)
    {
        var packageManager = new PluginPackageManager(options, loggerFactory.CreateLogger<PluginPackageManager>());
        var result = await packageManager.ExtractPackageAsync(package, ct);
        if (!result.IsValid)
            return (false, null, null, [.. result.Errors]);

        var manifest = result.Manifest!;
        var extractPath = result.ExtractPath!;

        var validator = new PluginAssemblyValidator(options, loggerFactory.CreateLogger<PluginAssemblyValidator>());
        var errors = validator.Validate(Path.Combine(extractPath, manifest.AssemblyFile));
        if (errors.Count > 0)
        {
            TryDeleteDirectory(extractPath);
            return (false, null, null, [.. errors]);
        }

        return (true, manifest, extractPath, []);
    }

    private async Task<InstallOutcome> LoadAndRegisterAsync(
        PluginManifest manifest,
        string extractPath,
        CancellationToken ct,
        bool keepDirectoryOnFailure = false)
    {
        var cleanup = keepDirectoryOnFailure
            ? static () => { }
            : new Action(() => TryDeleteDirectory(extractPath));

        var assemblyPath = Path.Combine(extractPath, manifest.AssemblyFile);
        var pluginId = manifest.Id;

        var context = new PluginLoadContext(pluginId, assemblyPath);
        Assembly assembly;
        IPlugin plugin;
        try
        {
            assembly = context.LoadFromAssemblyPath(assemblyPath);
            var entryPointType = assembly.GetType(manifest.EntryPoint, throwOnError: false);
            if (entryPointType is null || !typeof(IPlugin).IsAssignableFrom(entryPointType))
            {
                context.Unload();
                cleanup();
                return InstallOutcome.Failed([$"Entry point '{manifest.EntryPoint}' does not name an IPlugin implementation."]);
            }

            plugin = (IPlugin)Activator.CreateInstance(entryPointType)!;
            if (!string.Equals(plugin.Id, manifest.Id, StringComparison.Ordinal))
            {
                context.Unload();
                cleanup();
                return InstallOutcome.Failed([$"Plugin Id '{plugin.Id}' does not match manifest id '{manifest.Id}'."]);
            }
        }
        catch (Exception ex) when (ex is BadImageFormatException or ReflectionTypeLoadException or MissingMethodException)
        {
            logger.LogWarning(ex, "Plugin {PluginId} assembly could not be loaded", pluginId);
            context.Unload();
            cleanup();
            return InstallOutcome.Failed([$"Plugin assembly could not be loaded: {ex.Message}"]);
        }

        // Plugin's own migrations, inside its isolated plugins_{id} schema.
        var connectionString = configuration.GetConnectionString("Postgres") ?? string.Empty;
        try
        {
            var migrationRunner = new PluginMigrationRunner(loggerFactory.CreateLogger<PluginMigrationRunner>());
            await migrationRunner.ApplyAsync(pluginId, assembly, connectionString, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Migrations failed for {PluginId}", pluginId);
            context.Unload();
            cleanup();
            return InstallOutcome.Failed([$"Plugin migrations failed: {ex.Message}"]);
        }

        // Restricted service scope: the plugin registers into its own collection and
        // resolves only what it registered. Host services are never exposed here.
        // The host pre-registers the plugin's confined DbContext (schema plugins_{id})
        // so plugins never need -- and cannot change -- the connection details.
        ServiceProvider serviceProvider;
        try
        {
            var services = new ServiceCollection();
            services.AddScoped<IPluginDbContext>(_ => new PluginDbContext(pluginId, connectionString));
            plugin.ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Plugin {PluginId} failed during ConfigureServices", pluginId);
            context.Unload();
            cleanup();
            return InstallOutcome.Failed([$"Plugin service registration failed: {ex.Message}"]);
        }

        try
        {
            var menuItems = plugin.GetMenuItems().ToList();
            _loaded[pluginId] = new LoadedPlugin(plugin, context, serviceProvider, menuItems);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Plugin {PluginId} failed while contributing menu items", pluginId);
            await DisposeAsync(serviceProvider);
            context.Unload();
            cleanup();
            return InstallOutcome.Failed([$"Plugin failed to contribute menu items: {ex.Message}"]);
        }

        logger.LogInformation("Plugin {PluginId} v{Version} loaded and enabled", pluginId, plugin.Version);
        return InstallOutcome.Succeeded(manifest);
    }

    /// <summary>Disables a plugin: menu items unpublished, services disposed, ALC unloaded.</summary>
    public async Task DisableAsync(string pluginId)
    {
        if (!_loaded.TryRemove(pluginId, out var loaded))
            return;

        await DisposeAsync(loaded.ServiceProvider);
        loaded.Context.Unload();
        logger.LogInformation("Plugin {PluginId} disabled (context unloaded)", pluginId);
    }

    /// <summary>Uninstalls a plugin: disable + directory removal + schema drop.</summary>
    public async Task UninstallAsync(string pluginId, CancellationToken ct = default)
    {
        await DisableAsync(pluginId);

        var rootDirectory = options.Value.RootDirectory;
        TryDeleteDirectory(Path.Combine(rootDirectory, pluginId));

        var connectionString = configuration.GetConnectionString("Postgres") ?? string.Empty;
        var migrationRunner = new PluginMigrationRunner(loggerFactory.CreateLogger<PluginMigrationRunner>());
        await migrationRunner.DropSchemaAsync(pluginId, connectionString, ct);
    }

    private static async Task DisposeAsync(ServiceProvider provider)
    {
        await provider.DisposeAsync();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception)
        {
            // Best-effort: a leftover directory is surfaced by the next install attempt.
        }
    }

    /// <summary>Outcome of an install attempt: either a manifest or rejection reasons.</summary>
    public sealed record InstallOutcome(PluginManifest? Manifest, IReadOnlyList<string> Errors)
    {
        public bool Success => Manifest is not null && Errors.Count == 0;

        public static InstallOutcome Succeeded(PluginManifest manifest) => new(manifest, []);
        public static InstallOutcome Failed(IReadOnlyList<string> errors) => new(null, errors);
    }
}