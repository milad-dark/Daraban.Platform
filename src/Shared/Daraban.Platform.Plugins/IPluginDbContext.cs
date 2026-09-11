namespace Daraban.Platform.Plugins;

/// <summary>
/// The only database surface a plugin is allowed to touch. The host hands each plugin a
/// restricted context bound to the plugin's isolated <c>plugins_{id}</c> schema; plugins
/// have no way to reach the platform's connection string or DbContext.
/// </summary>
/// <remarks>
/// This is a thin abstraction over a raw ADO connection rather than a full EF model:
/// plugin tables are created by the plugin's own <see cref="IPluginMigration"/>s, so the
/// context exposes a small set of operations that keep plugin SQL confined to the plugin
/// schema.
/// </remarks>
public interface IPluginDbContext
{
    /// <summary>Executes a non-query SQL statement (DDL/DML) inside the plugin schema.</summary>
    /// <param name="sql">SQL text. Table references are resolved inside the plugin schema.</param>
    /// <param name="parameters">Positional parameters, if the SQL uses placeholders.</param>
    /// <returns>Number of rows affected.</returns>
    Task<int> ExecuteAsync(string sql, CancellationToken cancellationToken = default, params object[] parameters);

    /// <summary>
    /// Runs a query and maps every row to <c>T</c> via a (column-name, value) dictionary,
    /// keeping plugins decoupled from any host data library (Dapper, EF, ...).
    /// </summary>
    Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<IReadOnlyDictionary<string, object?>, T> map,
        CancellationToken cancellationToken = default);
}
