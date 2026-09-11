using System.IO.Compression;
using System.Text.Json;
using Daraban.Modules.Plugins.Services;
using Daraban.Platform.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daraban.Modules.Plugins.Tests;

/// <summary>
/// PluginPackageManager (Task 7.5): the zip-slip / zip-bomb / manifest gate. Extraction is
/// the single riskiest step of plugin installation (CWE-22, CWE-409), so every rule here
/// asserts a rejection that happens *before* any byte reaches the extraction directory.
/// </summary>
public class PluginPackageManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "daraban-plugin-tests", Guid.NewGuid().ToString("N"));
    private readonly PluginPackageManager _sut;

    public PluginPackageManagerTests()
    {
        Directory.CreateDirectory(_root);
        var options = Options.Create(new PluginsOptions { RootDirectory = _root });
        _sut = new PluginPackageManager(options, NullLogger<PluginPackageManager>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static byte[] Zip(params (string Name, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        return buffer.ToArray();
    }

    private static string ManifestJson(string? id = "asset-importer",
        string? type = "asset",
        string? name = "Asset Importer",
        string? version = "1.0.0",
        string? entryPoint = "AssetImporter.Plugin",
        string? assemblyFile = "AssetImporter.dll") =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["id"] = id,
            ["type"] = type,
            ["name"] = name,
            ["version"] = version,
            ["entryPoint"] = entryPoint,
            ["assemblyFile"] = assemblyFile,
        });

    // "MZ" magic bytes stand in for a real assembly; content is irrelevant to the packer.
    

    // ---- Valid package ------------------------------------------------------------------------

    [Fact]
    public async Task ExtractPackage_WithValidManifest_ExtractsToIdSubdirectory()
    {
        var zip = Zip(
            ("manifest.json", ManifestJson()),
            ("AssetImporter.dll", "MZ"));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        Assert.Equal("asset-importer", result.Manifest!.Id);
        Assert.True(Directory.Exists(Path.Combine(_root, "asset-importer")));
    }

    // ---- Zip-slip (CWE-22) ----------------------------------------------------------------------

    [Theory]
    [InlineData("../evil.dll")]                      // parent traversal
    [InlineData("..\\evil.dll")]                     // parent traversal, backslash
    [InlineData("../../etc/evil.dll")]               // deep traversal
    [InlineData("assets/../../evil.dll")]            // traversal mid-path
    [InlineData("/abs/evil.dll")]                    // rooted path
    [InlineData("\\\\server\\share\\evil.dll")]     // UNC path
    [InlineData("C:\\evil.dll")]                     // drive-absolute (device separator)
    [InlineData("assets/C:\\evil.dll")]              // drive mid-path
    [InlineData("")]                                 // empty name
    [InlineData("folder./file.dll")]                 // Windows-hostile component
    [InlineData("folder /file.dll")]                 // trailing space in component
    [InlineData("file.dll:stream")]                  // NTFS alternate data stream
    public void GetSafeEntryPath_RejectsEveryTraversalOrAbsoluteVariant(string entryName)
    {
        Assert.Null(PluginPackageManager.GetSafeEntryPath(entryName));
    }

    [Fact]
    public async Task ExtractPackage_WithTraversalEntry_RejectsAndWritesNothing()
    {
        var zip = Zip(
            ("manifest.json", ManifestJson()),
            ("../evil.dll", "escaped"));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("unsafe path"));
        Assert.False(Directory.Exists(Path.Combine(_root, "asset-importer")),
            "A rejected package must not leave an extraction directory behind.");
    }

    [Theory]
    [InlineData("manifest.json", "manifest.json")]
    [InlineData("assets/data.json", @"assets\data.json")]           // separators localized
    [InlineData("sub\\folder\\file.dll", @"sub\folder\file.dll")]    // backslash normalized
    public void GetSafeEntryPath_MapsSafeNamesToRelativePaths(string entryName, string expected)
    {
        var safe = PluginPackageManager.GetSafeEntryPath(entryName);
        Assert.Equal(expected, safe);
    }

    // ---- Manifest gate ---------------------------------------------------------------------------

    [Fact]
    public async Task ExtractPackage_WithoutManifest_Rejects()
    {
        var zip = Zip(("AssetImporter.dll", "MZ"));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("manifest.json"));
    }

    [Fact]
    public async Task ExtractPackage_WithInvalidManifestJson_Rejects()
    {
        var zip = Zip(
            ("manifest.json", "{ not json"),
            ("AssetImporter.dll", "MZ"));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("valid JSON"));
    }

    [Fact]
    public async Task ExtractPackage_WithInvalidManifestFields_RejectsBeforeExtraction()
    {
        // id with uppercase + traversal chars: must fail validation, not explode later.
        var manifest = ManifestJson(id: "Bad/ID", version: "not-semver", type: "unknown-kind");
        var zip = Zip(("manifest.json", manifest), ("AssetImporter.dll", "MZ"));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("manifest.id"));
        Assert.Contains(result.Errors, e => e.Contains("manifest.version"));
        Assert.Contains(result.Errors, e => e.Contains("manifest.type"));
        Assert.False(Directory.Exists(Path.Combine(_root, "Bad")),
            "Nothing may be extracted when the manifest is invalid.");
    }

    [Fact]
    public async Task ExtractPackage_WithUppercaseId_Rejects()
    {
        var zip = Zip(
            ("manifest.json", ManifestJson(id: "Asset-Importer")),
            ("AssetImporter.dll", "MZ"));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("manifest.id"));
    }

    [Fact]
    public async Task ExtractPackage_WithTraversingAssemblyFile_Rejects()
    {
        var zip = Zip(
            ("manifest.json", ManifestJson(assemblyFile: "../outside.dll")),
            ("AssetImporter.dll", "MZ"));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("manifest.assemblyFile"));
    }

    [Fact]
    public async Task ExtractPackage_WithMissingDeclaredAssembly_Rejects()
    {
        var zip = Zip(("manifest.json", ManifestJson(assemblyFile: "Missing.dll")));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Missing.dll"));
        Assert.False(Directory.Exists(Path.Combine(_root, "asset-importer")));
    }

    // ---- Size and entry limits ---------------------------------------------------------------------

    [Fact]
    public async Task ExtractPackage_WithEmptyStream_Rejects()
    {
        var result = await _sut.ExtractPackageAsync(new MemoryStream());

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public async Task ExtractPackage_ExceedingMaxPackageSize_Rejects()
    {
        var options = Options.Create(new PluginsOptions
        {
            RootDirectory = _root,
            MaxPackageSizeBytes = 10,
        });
        var sut = new PluginPackageManager(options, NullLogger<PluginPackageManager>.Instance);
        var zip = Zip(("manifest.json", ManifestJson()));
        var result = await sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("maximum size"));
    }

    [Fact]
    public async Task ExtractPackage_ExceedingEntryCount_Rejects()
    {
        var options = Options.Create(new PluginsOptions
        {
            RootDirectory = _root,
            MaxPackageEntries = 1,
        });
        var sut = new PluginPackageManager(options, NullLogger<PluginPackageManager>.Instance);
        var zip = Zip(("manifest.json", ManifestJson()), ("AssetImporter.dll", "MZ"));
        var result = await sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("entries"));
    }

    [Fact]
    public async Task ExtractPackage_ExceedingUncompressedCap_Rejects()
    {
        var options = Options.Create(new PluginsOptions
        {
            RootDirectory = _root,
            MaxUncompressedBytes = 12,
        });
        var sut = new PluginPackageManager(options, NullLogger<PluginPackageManager>.Instance);
        var zip = Zip(("manifest.json", ManifestJson()), ("big.txt", new string('x', 500)));
        var result = await sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("uncompressed"));
        Assert.False(Directory.Exists(Path.Combine(_root, "asset-importer")));
    }

    [Fact]
    public async Task ExtractPackage_WithExistingDirectory_RejectsWithConflict()
    {
        Directory.CreateDirectory(Path.Combine(_root, "asset-importer"));
        var zip = Zip(
            ("manifest.json", ManifestJson()),
            ("AssetImporter.dll", "MZ"));

        var result = await _sut.ExtractPackageAsync(new MemoryStream(zip));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("already exists"));
    }
}
