namespace Daraban.Platform.Plugins;

/// <summary>
/// SQL-capturing builder passed to <see cref="IPluginMigration.Up"/>. Operations are
/// recorded, not executed, so the host can wrap each migration in a transaction and
/// record it in the plugin's own migration-history table.
/// </summary>
/// <remarks>
/// <see cref="Sql(string)"/> takes finished SQL (DDL typically has no parameters).
/// <see cref="SqlFormat(FormattableString)"/> accepts an interpolated string whose
/// holes become real SQL parameters -- never concatenate values into SQL by hand.
/// </remarks>
public sealed class MigrationBuilder
{
    private readonly List<MigrationSqlOperation> _operations = [];

    /// <summary>Recorded operations, in the order they were added.</summary>
    public IReadOnlyList<MigrationSqlOperation> Operations => _operations;

    /// <summary>Enqueues literal SQL (typically DDL). Not executed until the host commits the migration.</summary>
    public void Sql(string sql) => _operations.Add(new MigrationSqlOperation(sql, []));

    /// <summary>
    /// Enqueues parameterized SQL. Interpolation holes become positional SQL parameters,
    /// e.g. <c>builder.SqlFormat($"INSERT INTO tags (name) VALUES ({name})")</c>.
    /// </summary>
    public void SqlFormat(FormattableString sql) =>
        _operations.Add(new MigrationSqlOperation(sql.Format, sql.GetArguments()));
}

/// <summary>One captured SQL statement plus its positional parameter values.</summary>
/// <param name="Sql">SQL text; <c>{0}</c>, <c>{1}</c>, … are parameter placeholders.</param>
/// <param name="Arguments">Values for the placeholders, in order.</param>
public sealed record MigrationSqlOperation(string Sql, object?[] Arguments);
