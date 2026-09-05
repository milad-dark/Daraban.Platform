using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Daraban.Modules.Knowledge.Data;

/// <summary>
/// Design-time entry point for `dotnet ef migrations add` / `dotnet ef database update`.
///
/// Without this, the EF tools have to boot a host project's whole DI graph just to obtain a
/// DbContext -- which for Host.Api means resolving JWT keys, RabbitMQ options and ten other
/// modules, none of which a migration needs. This factory lets the module's migrations be
/// generated from the Data project alone:
///
///   dotnet ef migrations add InitialKnowledge ^
///     -p src/Modules/Knowledge/Daraban.Modules.Knowledge.Data ^
///     -s src/Modules/Knowledge/Daraban.Modules.Knowledge.Data
///
/// The connection string here is only used to pick the provider and generate DDL; the design-time
/// tools never open it for `migrations add`. Override it with the DARABAN_MIGRATIONS_CONNECTION
/// environment variable when running `database update` against a real server.
/// </summary>
public sealed class KnowledgeDbContextFactory : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    private const string FallbackConnection =
        "Host=localhost;Port=5432;Database=daraban;Username=daraban;Password=daraban";

    public KnowledgeDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DARABAN_MIGRATIONS_CONNECTION")
                         ?? FallbackConnection;

        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(connection, npgsql =>
                // Must match AddKnowledgeModule, or migrations would be recorded in a different
                // history table than the one the running application checks.
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "knowledge"))
            .Options;

        return new KnowledgeDbContext(options);
    }
}
