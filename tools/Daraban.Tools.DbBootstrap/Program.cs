// =============================================================================
// Daraban.Platform — one-shot local/dev database bootstrap.
//
// Fresh-database story until the unified migration set lands (docs/06 §5):
// create the database if missing, create every module schema, then execute
// each module DbContext's GenerateCreateScript() in dependency order — the
// exact mechanism the integration fixture (tests/IntegrationTests) uses.
//
// Usage:
//   dotnet run --project tools/Daraban.Tools.DbBootstrap -- \
//     "Host=localhost;Port=5432;Database=daraban;Username=daraban;Password=..."
//
// Idempotency: GenerateCreateScript() emits plain CREATE TABLE (no IF NOT EXISTS),
// so before executing a context's script we check which of its tables already
// exist. All exist → skip; none → run; partial → skip with a loud warning
// (drop/recreate the database instead — statement-level merging is out of scope).
// =============================================================================
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Ordered the same way as the integration fixture. Contexts own distinct
// schemas so the scripts never collide; core.audit_logs is created by the
// Identity context's script (audit_logs is mapped into "core").
var contextTypes = new[]
{
    typeof(Daraban.Modules.Assets.Data.AssetsDbContext),
    typeof(Daraban.Modules.Automation.Data.AutomationDbContext),
    typeof(Daraban.Modules.Dashboard.Data.DashboardDbContext),
    typeof(Daraban.Modules.Discovery.Data.DiscoveryDbContext),
    typeof(Daraban.Modules.Financial.Data.FinancialDbContext),
    typeof(Daraban.Modules.Identity.Data.IdentityDbContext),
    typeof(Daraban.Modules.Inventory.Data.InventoryDbContext),
    typeof(Daraban.Modules.Knowledge.Data.KnowledgeDbContext),
    typeof(Daraban.Modules.Notifications.Data.NotificationsDbContext),
    typeof(Daraban.Modules.Plugins.Data.PluginsDbContext),
    typeof(Daraban.Modules.Reporting.Data.ReportingDbContext),
    typeof(Daraban.Modules.ServiceDesk.Data.ServiceDeskDbContext),
    typeof(Daraban.Modules.Settings.Data.SettingsDbContext),
    typeof(Daraban.Modules.Software.Data.SoftwareDbContext),
};

if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/Daraban.Tools.DbBootstrap -- \"<postgres connection string>\"");
    return 2;
}

var connectionString = args[0];
var builder = new NpgsqlConnectionStringBuilder(connectionString);

// ---- 1. Create the database itself if it does not exist ----------------------
var adminBuilder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
await using (var adminConnection = new NpgsqlConnection(adminBuilder.ConnectionString))
{
    await adminConnection.OpenAsync();
    var exists = await new NpgsqlCommand(
        "SELECT 1 FROM pg_database WHERE datname = @name",
        adminConnection) { Parameters = { new("name", builder.Database!) } }
        .ExecuteScalarAsync();
    if (exists is null)
    {
        // Identifier is quoted and comes from the connection string's Database field,
        // mirroring what psql -c 'CREATE DATABASE "name"' would do. Not injectable by
        // third parties: this is a local/dev bootstrap tool taking its own admin input.
        await new NpgsqlCommand($"CREATE DATABASE \"{builder.Database!.Replace("\"", "\"\"")}\"", adminConnection)
            .ExecuteNonQueryAsync();
        Console.WriteLine($"[db-bootstrap] created database '{builder.Database}'");
    }
    else
    {
        Console.WriteLine($"[db-bootstrap] database '{builder.Database}' already exists");
    }
}

// ---- 2. Create all module schemas --------------------------------------------
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();

string[] schemas =
[
    "assets", "automation", "dashboard", "discovery", "financial", "identity",
    "inventory", "knowledge", "notifications", "plugins", "reporting",
    "servicedesk", "settings", "software", "core"
];
foreach (var schema in schemas)
{
    await using var cmd = connection.CreateCommand();
    cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{schema}\"";
    await cmd.ExecuteNonQueryAsync();
}
Console.WriteLine($"[db-bootstrap] ensured {schemas.Length} schemas exist");

// ---- 3. Run every module context's create script ------------------------------
foreach (var contextType in contextTypes)
{
    // Every module context exposes a single ctor taking DbContextOptions<TContext>;
    // build the matching generic options via reflection so Activator finds it.
    var optionsMethod = typeof(SchemaBuilder)
        .GetMethod(nameof(SchemaBuilder.BuildOptionsFor), BindingFlags.NonPublic | BindingFlags.Static)!
        .MakeGenericMethod(contextType);
    var options = (DbContextOptions)optionsMethod.Invoke(null, [connectionString])!;

    using var context = (DbContext)Activator.CreateInstance(contextType, options)!;
    var script = context.Database.GenerateCreateScript();

    // Empty contexts (Automation/Notifications have no entities yet) produce empty
    // scripts — executing a blank command would throw.
    if (string.IsNullOrWhiteSpace(script))
    {
        Console.WriteLine($"[db-bootstrap] {contextType.Name}: no tables, skipped");
        continue;
    }

    // See header: skip contexts whose tables already exist (e.g. core.plugins created
    // by the PluginsSeeder on a previous host startup).
    var tables = ScriptAnalysis.ExtractTableNames(script);
    var existing = await ScriptAnalysis.ExistingTablesAsync(connection, tables);
    if (existing.Count == tables.Count)
    {
        Console.WriteLine($"[db-bootstrap] {contextType.Name}: all {tables.Count} table(s) already exist, skipped");
        continue;
    }
    if (existing.Count > 0)
    {
        Console.Error.WriteLine(
            $"[db-bootstrap] WARNING {contextType.Name}: partial schema — {existing.Count}/{tables.Count} tables exist " +
            $"({string.Join(", ", existing.Take(5))}...). Skipping this context; drop & re-bootstrap the database for a clean state.");
        continue;
    }

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = script;
    await cmd.ExecuteNonQueryAsync();

    Console.WriteLine($"[db-bootstrap] {contextType.Name}: {tables.Count} table(s)");
}

Console.WriteLine("[db-bootstrap] done — all module schemas created.");
return 0;

// Real static class (NOT a top-level local function): local functions get mangled
// compiler names and cannot be found via Type.GetMethod.
internal static class SchemaBuilder
{
    internal static DbContextOptions<T> BuildOptionsFor<T>(string connectionString) where T : DbContext =>
        new DbContextOptionsBuilder<T>()
            .UseNpgsql(connectionString)
            .Options;
}

internal static class ScriptAnalysis
{
    /// <summary>Schema-qualified table names a create script will create, e.g.
    /// "identity.users". Npgsql scripts quote identifiers: CREATE TABLE "s"."t" (...</summary>
    internal static IReadOnlyList<string> ExtractTableNames(string script)
    {
        var names = new List<string>();
        foreach (Match m in Regex.Matches(script, @"CREATE TABLE (?:IF NOT EXISTS )?""?([\w]+)""?\.(?:""?([\w]+)""?)"))
            names.Add($"{m.Groups[1].Value}.{m.Groups[2].Value}");
        return names;
    }

    /// <summary>Of the given schema-qualified names, the ones that already exist
    /// (pg_tables covers regular tables; we deliberately ignore views).</summary>
    internal static async Task<HashSet<string>> ExistingTablesAsync(NpgsqlConnection connection, IReadOnlyList<string> tables)
    {
        var found = new HashSet<string>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT table_schema || '.' || table_name FROM information_schema.tables";
        await using var reader = await cmd.ExecuteReaderAsync();
        var all = new HashSet<string>();
        while (await reader.ReadAsync())
            all.Add(reader.GetString(0));
        foreach (var t in tables)
            if (all.Contains(t))
                found.Add(t);
        return found;
    }
}
