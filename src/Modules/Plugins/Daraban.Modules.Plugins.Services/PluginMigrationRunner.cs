using System.Reflection;
using Daraban.Platform.Plugins;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Daraban.Modules.Plugins.Services;

/// <summary>
/// Executes a plugin's <see cref="IPluginMigration"/>s inside its isolated
/// <c>plugins_{id}</c> schema (Task 7.5). Migrations run in version order; each one is
/// recorded in the plugin's own <c>__plugin_migrations</c> table so upgrades re-run only
/// new migrations. The schema and the recording table are created by this runner.
/// </summary>
/// <remarks>
/// The SQL executed here comes from <see cref="MigrationBuilder"/> operations captured
/// from the plugin's own migration types -- the plugin never receives a connection, and
/// every statement runs with the connection's search_path confined to the plugin's
/// schema, so unqualified names create objects there, not in platform schemas.
/// </remarks>
public class PluginMigrationRunner(ILogger<PluginMigrationRunner> logger)
{
    /// <summary>Applies all pending migrations for a loaded plugin assembly.</summary>
    public async Task ApplyAsync(string pluginId, Assembly pluginAssembly, string connectionString, CancellationToken ct = default)
    {
        var schemaName = GetSchemaName(pluginId);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // Isolated schema + recording table, idempotent.
        await ExecuteAsync(connection, ct, $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\"");
        await ExecuteAsync(connection, ct, $"""
            CREATE TABLE IF NOT EXISTS "{schemaName}".__plugin_migrations (
                name text NOT NULL,
                applied_at timestamptz NOT NULL DEFAULT now(),
                CONSTRAINT pk_plugin_migrations PRIMARY KEY (name)
            )
            """);

        var applied = await LoadAppliedNamesAsync(connection, schemaName, ct);

        // Discover IPluginMigration implementations in the plugin assembly only.
        var migrations = pluginAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && typeof(IPluginMigration).IsAssignableFrom(t))
            .Select(t => (IPluginMigration?)Activator.CreateInstance(t))
            .Where(m => m is not null)
            .Select(m => m!)
            .OrderBy(m => m.Order)
            .ToList();

        foreach (var migration in migrations)
        {
            var name = migration.GetType().FullName!;
            if (applied.Contains(name))
                continue;

            logger.LogInformation("Applying plugin migration {Migration} for {PluginId}", name, pluginId);

            var builder = new MigrationBuilder();
            migration.Up(builder);

            await using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                foreach (var operation in builder.Operations)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = operation.Sql;

                    for (var i = 0; i < operation.Arguments.Length; i++)
                        command.Parameters.AddWithValue(new NpgsqlParameter($"p{i}", operation.Arguments[i] ?? DBNull.Value));

                    await command.ExecuteNonQueryAsync(ct);
                }

                await RecordAsync(connection, schemaName, name, ct, transaction);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }

    /// <summary>Drops a plugin's isolated schema along with every table it contains.</summary>
    public async Task DropSchemaAsync(string pluginId, string connectionString, CancellationToken ct = default)
    {
        var name = GetSchemaName(pluginId);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await ExecuteAsync(connection, ct, $"DROP SCHEMA IF EXISTS \"{name}\" CASCADE");
        logger.LogInformation("Dropped plugin schema {Schema} for {PluginId}", name, pluginId);
    }

    /// <summary>Plugin ids validated by <c>PluginManifest.IdPattern</c> are lowercase
    /// alphanumerics and dashes; underscores here are safe and collision-free.</summary>
    internal static string GetSchemaName(string pluginId) => $"plugins_{pluginId.Replace('-', '_')}";

    private static async Task<HashSet<string>> LoadAppliedNamesAsync(NpgsqlConnection connection, string schemaName, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT name FROM \"{schemaName}\".__plugin_migrations";
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            applied.Add(reader.GetString(0));
        return applied;
    }

    private static async Task RecordAsync(NpgsqlConnection connection, string schemaName, string name, CancellationToken ct, NpgsqlTransaction transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO \"{schemaName}\".__plugin_migrations (name) VALUES (@name) ON CONFLICT DO NOTHING";
        command.Parameters.AddWithValue("name", name);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, CancellationToken ct, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        if (transaction is not null)
            command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}