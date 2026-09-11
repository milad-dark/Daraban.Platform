using System.Text.Json;
using Daraban.Modules.Plugins.Data.Entities;
using Daraban.Modules.Plugins.Data.Repositories;
using Daraban.Platform.Common;
using Daraban.Platform.Plugins;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Plugins.Services;

/// <summary>Business surface of the plugin registry (Task 7.5), consumed by the API layer.</summary>
public interface IPluginService
{
    Task<IReadOnlyList<Plugin>> ListAsync(CancellationToken ct = default);

    Task<Result<Plugin>> GetAsync(string pluginId, CancellationToken ct = default);

    Task<Result<Plugin>> InstallAsync(Stream package, Guid? actorId, CancellationToken ct = default);

    Task<Result<Plugin>> EnableAsync(string pluginId, Guid? actorId, CancellationToken ct = default);

    Task<Result<Plugin>> DisableAsync(string pluginId, Guid? actorId, CancellationToken ct = default);

    Task<Result<Plugin>> UninstallAsync(string pluginId, Guid? actorId, CancellationToken ct = default);

    /// <summary>Merged menu items from every enabled plugin, for the shell.</summary>
    IReadOnlyList<PluginMenuItem> GetMenuItems();
}

/// <summary>
/// Business surface of the plugin registry (Task 7.5): list/get/install/enable/
/// disable/uninstall, Result-pattern all the way down (ADR-003). All mutating
/// operations are admin-gated at the API layer (plugins.write permission); this
/// service enforces the registry-state machine on top. The manager owns runtime
/// state; the repository owns persistence; this class never touches either directly
/// beyond those two collaborators.
/// </summary>
public class PluginService(
    IPluginRepository repository,
    IPluginManager manager,
    ILogger<PluginService> logger) : IPluginService
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc />
    public async Task<IReadOnlyList<Plugin>> ListAsync(CancellationToken ct = default) =>
        await repository.GetActiveAsync(ct);

    /// <inheritdoc />
    public async Task<Result<Plugin>> GetAsync(string pluginId, CancellationToken ct = default)
    {
        var plugin = await repository.GetByPluginIdAsync(pluginId, ct);
        return plugin is null
            ? Result.Failure<Plugin>(NotFound(pluginId))
            : Result.Success(plugin);
    }

    /// <inheritdoc />
    public async Task<Result<Plugin>> InstallAsync(Stream package, Guid? actorId, CancellationToken ct = default)
    {
        var outcome = await manager.InstallAsync(package, ct);
        if (!outcome.Success)
        {
            logger.LogWarning("Plugin install rejected: {Errors}", string.Join("; ", outcome.Errors));
            return Result.Failure<Plugin>(new Error(
                "PLUGINS.PACKAGE_INVALID", string.Join(" ", outcome.Errors), ErrorType.Validation));
        }

        var manifest = outcome.Manifest!;
        var existing = await repository.GetByPluginIdAsync(manifest.Id, ct);
        if (existing is not null && existing.Status != "uninstalled")
        {
            // Extraction refuses existing directories, so the runtime state is clean;
            // only the registry row (from an earlier install) needs a conflict answer.
            await manager.UninstallAsync(manifest.Id, ct);
            return Result.Failure<Plugin>(new Error(
                "PLUGINS.ALREADY_INSTALLED",
                $"Plugin '{manifest.Id}' is already installed; uninstall it first.", ErrorType.Conflict));
        }

        var plugin = new Plugin
        {
            PluginId = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            Type = manifest.Type,
            Status = PluginManager.EnabledStatus,
            InstalledAt = DateTimeOffset.UtcNow,
            ManifestJson = JsonSerializer.Serialize(manifest, ManifestJson),
            CreatedById = actorId,
        };

        await repository.AddAsync(plugin, ct);
        await repository.SaveChangesAsync(ct);
        logger.LogInformation("Plugin {PluginId} v{Version} registered", manifest.Id, manifest.Version);
        return Result.Success(plugin);
    }

    /// <inheritdoc />
    public async Task<Result<Plugin>> EnableAsync(string pluginId, Guid? actorId, CancellationToken ct = default)
    {
        var row = await GetRowOrFailure(pluginId, ct);
        if (!row.IsSuccess) return row;

        // A tombstone can only be replaced by a fresh install, never re-enabled.
        if (row.Value.Status == PluginManager.UninstalledStatus)
            return InvalidState(row.Value, "uninstalled; install it again first");

        if (row.Value.Status == PluginManager.EnabledStatus)
            return InvalidState(row.Value, "already enabled");

        // The package stays on disk while disabled; enabling reloads it from there.
        var reload = await manager.InstallFromDirectoryAsync(pluginId, ct);
        if (!reload.Success)
            return Result.Failure<Plugin>(new Error(
                "PLUGINS.PACKAGE_INVALID", string.Join(" ", reload.Errors), ErrorType.Validation));

        row.Value.Status = PluginManager.EnabledStatus;
        row.Value.UpdatedById = actorId;
        await repository.SaveChangesAsync(ct);
        return row;
    }

    /// <inheritdoc />
    public async Task<Result<Plugin>> DisableAsync(string pluginId, Guid? actorId, CancellationToken ct = default)
    {
        var row = await GetRowOrFailure(pluginId, ct);
        if (!row.IsSuccess) return row;

        // A tombstone can only be replaced by a fresh install, never resurrected.
        if (row.Value.Status == PluginManager.UninstalledStatus)
            return InvalidState(row.Value, "uninstalled; install it again first");

        if (row.Value.Status == PluginManager.DisabledStatus)
            return InvalidState(row.Value, "already disabled");

        await manager.DisableAsync(pluginId);
        row.Value.Status = PluginManager.DisabledStatus;
        row.Value.UpdatedById = actorId;
        await repository.SaveChangesAsync(ct);
        return row;
    }

    /// <inheritdoc />
    public async Task<Result<Plugin>> UninstallAsync(string pluginId, Guid? actorId, CancellationToken ct = default)
    {
        var row = await GetRowOrFailure(pluginId, ct);
        if (!row.IsSuccess) return row;

        if (row.Value.Status == PluginManager.UninstalledStatus)
            return InvalidState(row.Value, "already uninstalled");

        await manager.UninstallAsync(pluginId, ct);
        row.Value.Status = PluginManager.UninstalledStatus;
        row.Value.UpdatedById = actorId;
        await repository.SaveChangesAsync(ct);
        return row;
    }

    /// <inheritdoc />
    public IReadOnlyList<PluginMenuItem> GetMenuItems() => manager.MenuItems;

    private async Task<Result<Plugin>> GetRowOrFailure(string pluginId, CancellationToken ct)
    {
        var plugin = await repository.GetByPluginIdAsync(pluginId, ct);
        return plugin is null
            ? Result.Failure<Plugin>(NotFound(pluginId))
            : Result.Success(plugin);
    }

    private static Error NotFound(string pluginId) =>
        new("PLUGINS.NOT_FOUND", $"Plugin '{pluginId}' is not installed.", ErrorType.NotFound);

    private static Result<Plugin> InvalidState(Plugin plugin, string reason) =>
        Result.Failure<Plugin>(new Error(
            "PLUGINS.INVALID_STATE", $"Plugin '{plugin.PluginId}' is {reason}.", ErrorType.Conflict));
}