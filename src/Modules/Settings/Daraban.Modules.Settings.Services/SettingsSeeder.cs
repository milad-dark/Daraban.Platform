using System.Text.Json;
using Daraban.Modules.Settings.Data;
using Daraban.Modules.Settings.Data.Repositories;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Daraban.Modules.Settings.Services;

/// <summary>
/// Ensures the settings table exists and matches <see cref="SettingCatalog"/> (Task 7.4).
/// Runs once at startup, before any request can touch the settings API:
/// 1. creates the table/indexes and seeds the catalog rows idempotently (ON CONFLICT DO
///    NOTHING, so operator-modified values survive re-runs), then
/// 2. removes rows whose key no longer exists in the catalog, so a renamed/removed setting
///    does not linger as a zombie row after an upgrade.
/// </summary>
/// <remarks>
/// The DDL ships as idempotent SQL rather than an EF migration because this module (like
/// every module except Knowledge) does not carry a migrations assembly yet; the statements
/// are safe to run on every startup and convert to a migration unchanged.
/// </remarks>
public class SettingsSeeder(
    ISystemSettingRepository repository,
    string connectionString,
    ILogger<SettingsSeeder> logger)
{
    private readonly string _connectionString = connectionString;

    public virtual async Task SeedAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var seedCommand = connection.CreateCommand();
        seedCommand.CommandText = SettingCatalog.BuildSeedSql();
        await seedCommand.ExecuteNonQueryAsync(ct);

        await RemoveOrphansAsync(connection, ct);
    }

    /// <summary>Deletes rows whose key is no longer in the catalog. Keys are bound as a
    /// parameter (parameterized array, never string-concatenated); the table name is a
    /// compile-time literal.</summary>
    private async Task RemoveOrphansAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var knownKeys = SettingCatalog.All.Select(d => d.Key).ToArray();
        var existing = (await repository.GetAllAsync(ct)).Select(s => s.Key).ToArray();
        var orphans = existing.Except(knownKeys).ToArray();
        if (orphans.Length == 0)
        {
            return;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM core.system_settings WHERE key = ANY(@keys)";
        command.Parameters.Add(new NpgsqlParameter("keys", orphans));
        await command.ExecuteNonQueryAsync(ct);

        logger.LogInformation("Removed {Count} orphaned setting(s): {Keys}", orphans.Length, string.Join(", ", orphans));
    }

    /// <summary>Stable fingerprint of the catalog definition set -- usable by hosts to
    /// distinguish a cache written by a different process version from a current one.</summary>
    internal static string ComputeFingerprint()
    {
        var payload = JsonSerializer.Serialize(
            SettingCatalog.All.Select(d => new { d.Key, d.Default, Type = d.Type.ToString(), d.Category, d.IsSecret }));

        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
