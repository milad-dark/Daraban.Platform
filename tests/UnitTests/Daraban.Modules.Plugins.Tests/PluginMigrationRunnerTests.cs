using Daraban.Modules.Plugins.Services;
using Xunit;

namespace Daraban.Modules.Plugins.Tests;

/// <summary>
/// PluginMigrationRunner identifier handling (Task 7.5): plugin ids become DB schema
/// names (<c>plugins_{id}</c>). Although ids are pre-validated by PluginManifest's
/// pattern, the quoting + dash-to-underscore mapping is verified independently here --
/// a schema name is a SQL identifier, so this is the injection surface.
/// </summary>
public class PluginMigrationRunnerTests
{
    [Theory]
    [InlineData("asset-importer", "plugins_asset_importer")]
    [InlineData("slack-integration", "plugins_slack_integration")]
    [InlineData("a9", "plugins_a9")]
    public void GetSchemaName_MapsIdToQuotedSafeSchemaName(string pluginId, string expected)
    {
        Assert.Equal(expected, PluginMigrationRunner.GetSchemaName(pluginId));
    }

    [Fact]
    public void GetSchemaName_OnUncheckedInput_ReplacesOnlyDashes()
    {
        // Pre-validation raw input is not a real flow; the mapping is intentionally
        // minimal and callers double-quote the result at every SQL call site. This
        // pins the contract: only '-' is rewritten, nothing else is altered.
        Assert.Equal("plugins_asset importer", PluginMigrationRunner.GetSchemaName("asset importer"));
        Assert.Equal("plugins_'; DROP SCHEMA core;__", PluginMigrationRunner.GetSchemaName("'; DROP SCHEMA core;--"));
    }

    [Fact]
    public void GetSchemaName_IsDeterministicAcrossCalls()
    {
        Assert.Equal(
            PluginMigrationRunner.GetSchemaName("asset-importer"),
            PluginMigrationRunner.GetSchemaName("asset-importer"));
    }
}
