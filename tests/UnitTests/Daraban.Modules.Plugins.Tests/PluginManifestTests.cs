using Daraban.Platform.Plugins;
using Xunit;

namespace Daraban.Modules.Plugins.Tests;

/// <summary>
/// PluginManifest.Validate (Task 7.5): the first line of defense. Every field is checked
/// *before* any assembly is loaded -- ids feed directory and DB-schema names, so the
/// pattern must reject anything that could escape the plugins root or inject SQL.
/// </summary>
public class PluginManifestTests
{
    private static PluginManifest Valid(string? id = "asset-importer", string? type = PluginTypes.Asset) => new()
    {
        Id = id!,
        Type = type!,
        Name = "Asset Importer",
        Version = "1.2.3",
        EntryPoint = "AssetImporter.Plugin",
        AssemblyFile = "AssetImporter.dll",
    };

    // ---- Id (directory + schema name; CWE-22 / SQL-injection via identifiers) -----------------

    [Theory]
    [InlineData("asset-importer")]        // canonical
    [InlineData("a9b")]                    // shortest legal (3 chars)
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")] // 62 chars + pattern
    [InlineData("integrations-slack-ldap")]
    public void Validate_Accepts_LegalIds(string id)
    {
        Assert.Empty(Valid(id).Validate());
    }

    [Theory]
    [InlineData("Asset-Importer")]        // uppercase
    [InlineData("asset importer")]        // whitespace
    [InlineData("asset_importer")]        // underscore
    [InlineData("asset/importer")]         // path separator
    [InlineData("asset\\importer")]       // path separator (win)
    [InlineData("../escape")]             // traversal
    [InlineData("..")]                    // traversal
    [InlineData("a")]                    // too short (pattern requires 3+)
    [InlineData("-leading")]              // leading dash
    [InlineData("trailing-")]             // trailing dash
    [InlineData("with:colon")]
    [InlineData("with;semicolon")]
    [InlineData("with's-quote")]
    [InlineData("with\"d-quote")]
    [InlineData("plugins;DROP TABLE x;--")]
    [InlineData("")]
    public void Validate_Rejects_DangerousOrMalformedIds(string id)
    {
        Assert.Contains(Valid(id).Validate(), e => e.Contains("manifest.id"));
    }

    // ---- Type -----------------------------------------------------------------------------------

    [Theory]
    [InlineData(PluginTypes.Asset)]
    [InlineData(PluginTypes.TicketAutomation)]
    [InlineData(PluginTypes.Report)]
    [InlineData(PluginTypes.Integration)]
    public void Validate_Accepts_TheFourPluginTypes(string type)
    {
        Assert.Empty(Valid(type: type).Validate());
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("shell")]
    [InlineData("")]
    public void Validate_Rejects_UnknownTypes(string type)
    {
        Assert.Contains(Valid(type: type).Validate(), e => e.Contains("manifest.type"));
    }

    // ---- Version / EntryPoint / AssemblyFile ---------------------------------------------------

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("10.20.30")]
    [InlineData("1.0.0-beta.1")]
    public void Validate_Accepts_SemverVersions(string version)
    {
        Assert.Empty(Valid().Validate());
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1.0")]
    [InlineData("v1.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("latest")]
    [InlineData("1.0.0; DROP SCHEMA core")]
    public void Validate_Rejects_NonSemverVersions(string version)
    {
        var manifest = Valid();
        manifest.Version = version;
        Assert.Contains(manifest.Validate(), e => e.Contains("manifest.version"));
    }

    [Theory]
    [InlineData("../Outside.dll")]
    [InlineData("sub/dir/Plugin.dll")]
    [InlineData("Plugin.exe")]
    [InlineData("Plugin")]
    [InlineData("Plugin.dll.exe")]
    [InlineData("PLUGIN.DLL")]
    [InlineData("Plugin.zip")]
    [InlineData(".dll")]
    [InlineData("")]
    public void Validate_Rejects_UnsafeAssemblyFiles(string assemblyFile)
    {
        var manifest = Valid();
        manifest.AssemblyFile = assemblyFile;
        Assert.Contains(manifest.Validate(), e => e.Contains("manifest.assemblyFile"));
    }

    [Fact]
    public void Validate_Accepts_PlainDllFileName()
    {
        var manifest = Valid();
        manifest.AssemblyFile = "AssetImporter.Plugin.v12.dll";
        Assert.Empty(manifest.Validate());
    }

    // ---- EntryPoint ---------------------------------------------------------------------------------

    [Fact]
    public void Validate_Rejects_TypeNameWithAssemblyQualifiers()
    {
        // "Type, Assembly" lets GetType load from an arbitrary assembly -- reject.
        var manifest = Valid();
        manifest.EntryPoint = "AssetImporter.Plugin, Evil.Assembly";
        Assert.Contains(manifest.Validate(), e => e.Contains("manifest.entryPoint"));
    }

    [Fact]
    public void Validate_Rejects_EmptyEntryPoint()
    {
        var manifest = Valid();
        manifest.EntryPoint = "";
        Assert.Contains(manifest.Validate(), e => e.Contains("manifest.entryPoint"));
    }

    // ---- Name / RemoteEntryUrl ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Rejects_MissingName(string? name)
    {
        var manifest = Valid();
        manifest.Name = name!;
        Assert.Contains(manifest.Validate(), e => e.Contains("manifest.name"));
    }

    [Fact]
    public void Validate_Rejects_OverlongName()
    {
        var manifest = Valid();
        manifest.Name = new string('n', 101);
        Assert.Contains(manifest.Validate(), e => e.Contains("manifest.name"));
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative/path")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://plugins.example.com/x.js")]
    public void Validate_Rejects_RelativeOrNonHttpRemoteEntryUrl(string url)
    {
        var manifest = Valid();
        manifest.RemoteEntryUrl = url;
        Assert.Contains(manifest.Validate(), e => e.Contains("manifest.remoteEntryUrl"));
    }

    [Fact]
    public void Validate_Accepts_AbsoluteRemoteEntryUrl()
    {
        var manifest = Valid();
        manifest.RemoteEntryUrl = "https://plugins.daraban.local/asset-importer/remoteEntry.js";
        Assert.Empty(manifest.Validate());
    }
}
