namespace Daraban.Platform.Plugins;

/// <summary>
/// A single schema migration owned by a plugin. The plugin assembly can contain any
/// number of <c>IPluginMigration</c> implementations; the host executes their
/// <see cref="Up"/> methods in order during installation (and re-runs any that have
/// not yet been recorded) against the plugin's isolated schema.
/// </summary>
/// <remarks>
/// The builder handed to <see cref="Up"/> captures SQL that the host executes inside
/// the plugin's private <c>plugins_{id}</c> schema (the connection's search_path is
/// pointed there first) -- plugins cannot create tables in platform schemas without
/// explicitly naming them.
/// </remarks>
public interface IPluginMigration
{
    /// <summary>Monotonic sequence number; the host orders migrations by this value.</summary>
    int Order { get; }

    /// <summary>Applies the migration to the plugin's own isolated DB schema.</summary>
    void Up(MigrationBuilder builder);
}
