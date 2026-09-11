using System.Data;
using Daraban.Platform.Plugins;
using Npgsql;

namespace Daraban.Modules.Plugins.Services.Runtime;

/// <summary>
/// The only data door plugins get (Task 7.5: "no direct DB access -- must use provided
/// IPluginDbContext"). Each instance wraps a dedicated NpgsqlConnection whose
/// search_path is confined to the plugin's private plugins_{id} schema, so
/// unqualified table names resolve there and cross-schema queries must name the
/// schema explicitly. Every SQL text plugin's hand in is executed as a prepared,
/// parameterized command -- the interface takes no parameter objects by design.
/// </summary>
public sealed class PluginDbContext(string pluginId, string connectionString) : IPluginDbContext, IAsyncDisposable
{
    private NpgsqlConnection? _connection;

    private async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is null)
        {
            var connection = new NpgsqlConnection(BuildConnectionString(pluginId, connectionString));
            await connection.OpenAsync(ct);
            _connection = connection;
        }

        return _connection;
    }

    /// <summary>Confines the connection to the plugin's schema via search_path.</summary>
    internal static string BuildConnectionString(string pluginId, string connectionString)
    {
        var schemaName = $"plugins_{pluginId.Replace('-', '_')}";
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            // Single schema: unqualified names resolve only inside the plugin schema.
            SearchPath = schemaName,
        };
        return builder.ConnectionString;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(string sql, CancellationToken ct = default, params object[] parameters)
    {
        var connection = await GetConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<IReadOnlyDictionary<string, object?>, T> map, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var results = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        do
        {
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                results.Add(map(row));
            }
        } while (await reader.NextResultAsync(ct));
        return results;
    }

    private static void AddParameters(NpgsqlCommand command, object[] parameters)
    {
        for (var i = 0; i < parameters.Length; i++)
            command.Parameters.AddWithValue(new NpgsqlParameter($"p{i}", parameters[i] ?? DBNull.Value));
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}