namespace Daraban.Modules.Plugins.Services.Dtos;

/// <summary>Read view of a plugin registry row. The raw manifest JSON is echoed for the
/// UI; sensitive values never occur there because the manifest is validated at install
/// time and stores only id/name/version/type/entry-point/assembly metadata.</summary>
public sealed record PluginDto(
    Guid Id,
    string PluginId,
    string Name,
    string Version,
    string Type,
    string Status,
    DateTimeOffset InstalledAt,
    string? ManifestJson);

/// <summary>Merged menu items contributed by all enabled plugins, for the Angular shell.
/// <paramref name="RequiredPermission"/> lets the shell hide entries the user lacks;
/// the plugin API also filters server-side.</summary>
public sealed record PluginMenuItemDto(
    string Title,
    string Route,
    string Icon,
    int Order,
    string? RequiredPermission);

/// <summary>Maps a registry entity to its API DTO. Entities are never returned directly
/// (no EF-shape leakage, no audit internals).</summary>
public static class PluginDtoMapper
{
    public static PluginDto ToDto(this Data.Entities.Plugin plugin) => new(
        plugin.Id,
        plugin.PluginId,
        plugin.Name,
        plugin.Version,
        plugin.Type,
        plugin.Status,
        plugin.InstalledAt,
        plugin.ManifestJson);
}