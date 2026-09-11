using System.Text.Json;
using Daraban.Modules.Plugins.Data.Entities;
using Daraban.Modules.Plugins.Data.Repositories;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Daraban.Modules.Plugins.Services;

/// <summary>
/// Creates the <c>core.plugins</c> registry table via idempotent raw DDL at host
/// startup, mirroring the Settings module's approach for core.* tables: EF owns the
/// model, but the table itself is ensured by this seeder, not an EF migration,
/// because the module owns no migration assembly (Task 7.5 DB layer).
/// </summary>
public class PluginsSeeder(
    string connectionString,
    ILogger<PluginsSeeder> logger)
{
    /// <summary>Ensures the core.plugins table, its unique key, and status index exist.</summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("No Postgres connection string configured; skipping core.plugins DDL.");
            return;
        }

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await ExecuteAsync(connection, ct, "CREATE SCHEMA IF NOT EXISTS core");

        await ExecuteAsync(connection, ct, """
            CREATE TABLE IF NOT EXISTS core.plugins (
                id uuid NOT NULL,
                plugin_id varchar(64) NOT NULL,
                name varchar(100) NOT NULL,
                version varchar(32) NOT NULL,
                type varchar(32) NOT NULL,
                status varchar(16) NOT NULL,
                installed_at timestamptz NOT NULL,
                manifest_json jsonb NOT NULL,
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now(),
                created_by_id uuid NULL,
                updated_by_id uuid NULL,
                CONSTRAINT pk_core_plugins PRIMARY KEY (id)
            )
            """);

        await ExecuteAsync(connection, ct, """
            CREATE UNIQUE INDEX IF NOT EXISTS uq_core_plugins_plugin_id
                ON core.plugins (plugin_id)
            """);

        await ExecuteAsync(connection, ct, """
            CREATE INDEX IF NOT EXISTS ix_core_plugins_status
                ON core.plugins (status)
            """);

        logger.LogInformation("core.plugins table ensured");
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, CancellationToken ct, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}