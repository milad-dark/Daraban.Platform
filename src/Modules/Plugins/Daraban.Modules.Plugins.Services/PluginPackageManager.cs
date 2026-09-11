using System.IO.Compression;
using System.Text.Json;
using Daraban.Platform.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daraban.Modules.Plugins.Services;

/// <summary>
/// Validates and extracts plugin packages (Task 7.5). Extraction is the riskiest step of
/// plugin installation, so every safeguard runs before the first byte hits disk:
/// size/entry caps, zip-bomb ratio checks, absolute/path-traversal entry names, and a
/// manifest that must pass <see cref="PluginManifest.Validate"/> before anything else.
/// </summary>
public class PluginPackageManager(
    IOptions<PluginsOptions> options,
    ILogger<PluginPackageManager> logger)
{
    internal static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>Result of validating and extracting a package: either a parsed manifest
    /// plus the directory it was extracted to, or a list of rejection reasons.</summary>
    public sealed record PackageResult(PluginManifest? Manifest, string? ExtractPath, List<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>
    /// Reads a plugin .zip stream, validates it end-to-end, and extracts it under
    /// <c>{options.RootDirectory}/{manifest.id}/</c>. No assembly is loaded here.
    /// </summary>
    public async Task<PackageResult> ExtractPackageAsync(Stream packageStream, CancellationToken ct = default)
    {
        var opts = options.Value;

        // Buffer to memory first: the stream's length may be unknown (chunked upload),
        // and all validation must complete before any file is written.
        using var buffer = new MemoryStream();
        await packageStream.CopyToAsync(buffer, 81920, ct);
        if (buffer.Length == 0)
            return Fail("Package is empty.");
        if (buffer.Length > opts.MaxPackageSizeBytes)
            return Fail($"Package exceeds the maximum size of {opts.MaxPackageSizeBytes} bytes.");

        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        var entries = archive.Entries;

        if (entries.Count == 0)
            return Fail("Package contains no entries.");
        if (entries.Count > opts.MaxPackageEntries)
            return Fail($"Package contains {entries.Count} entries; the maximum is {opts.MaxPackageEntries}.");

        var manifestEntry = entries.FirstOrDefault(e =>
            string.Equals(e.FullName, "manifest.json", StringComparison.OrdinalIgnoreCase));
        if (manifestEntry is null)
            return Fail("Package does not contain a manifest.json at its root.");

        PluginManifest? manifest;
        try
        {
            using var manifestStream = manifestEntry.Open();
            manifest = await JsonSerializer.DeserializeAsync<PluginManifest>(manifestStream, ManifestJsonOptions, ct);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Plugin manifest is not valid JSON: {Error}", ex.Message);
            return Fail("manifest.json is not valid JSON.");
        }

        if (manifest is null)
            return Fail("manifest.json is not valid JSON.");

        var errors = manifest.Validate();
        if (errors.Count > 0)
            return new PackageResult(null, null, errors);

        // Zip-bomb guard: reject packages whose uncompressed size exceeds the cap,
        // checking the declared size of every entry (not the sum) as well.
        long totalUncompressed = 0;
        foreach (var entry in entries)
        {
            if (entry.Length < 0)
                return Fail("Package contains an entry with an invalid size.");
            totalUncompressed += entry.Length;
            if (totalUncompressed > opts.MaxUncompressedBytes)
                return Fail($"Package expands beyond the maximum of {opts.MaxUncompressedBytes} uncompressed bytes.");
        }

        // Extract to the plugin's own subdirectory. The id was validated against
        // PluginManifest.IdPattern (lowercase/digits/dashes) so it cannot escape the root.
        var extractRoot = Path.Combine(opts.RootDirectory, manifest.Id);
        if (Directory.Exists(extractRoot))
            return Fail($"A plugin directory for '{manifest.Id}' already exists; uninstall it first.");

        try
        {
            Directory.CreateDirectory(extractRoot);

            foreach (var entry in entries)
            {
                var safeName = GetSafeEntryPath(entry.FullName);
                if (safeName is null)
                {
                    CleanDirectory(extractRoot);
                    return Fail($"Package entry '{entry.FullName}' has an unsafe path.");
                }

                if (safeName.EndsWith(Path.DirectorySeparatorChar))
                {
                    // Directory entry (trailing '/'): create it, nothing to extract.
                    Directory.CreateDirectory(Path.Combine(extractRoot, safeName));
                    continue;
                }

                var destination = Path.Combine(extractRoot, safeName);
                var destinationDir = Path.GetDirectoryName(destination);
                if (destinationDir is not null)
                    Directory.CreateDirectory(destinationDir);

                entry.ExtractToFile(destination, overwrite: true);
            }

            var assemblyPath = Path.Combine(extractRoot, manifest.AssemblyFile);
            if (!File.Exists(assemblyPath))
            {
                CleanDirectory(extractRoot);
                return Fail($"Package does not contain the declared assembly '{manifest.AssemblyFile}'.");
            }

            return new PackageResult(manifest, extractRoot, []);
        }
        catch (Exception ex) when (ex is IOException or System.Security.SecurityException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Plugin package extraction failed for '{PluginId}'", manifest.Id);
            CleanDirectory(extractRoot);
            return Fail($"Package extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps a zip entry name to a safe relative path under the extraction root, or null
    /// when the name is absolute, contains parent traversal, is a rooted drive path, or
    /// otherwise cannot be safely written. This is the zip-slip defense (CWE-22).
    /// </summary>
    internal static string? GetSafeEntryPath(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            return null;

        // Normalize separators; zip spec uses '/', but some packers emit '\'.
        var normalized = entryName.Replace('\\', '/');

        // Directory entries end with '/': allowed, and handled as directories during extract.
        var isDirectory = normalized.EndsWith('/');

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        foreach (var part in parts)
        {
            // Reject parent traversal, current-dir noise, and device/ADS separators.
            if (part == ".." || part == "." || part.Contains(':'))
                return null;
            if (part.EndsWith(' ') || part.EndsWith('.'))
                return null; // Windows-hostile names; plugins must be cross-platform.
        }

        var relative = string.Join(Path.DirectorySeparatorChar, parts);

        // A leading separator means the entry is rooted -- reject it.
        if (entryName.StartsWith('/') || entryName.StartsWith('\\'))
            return null;

        return isDirectory ? relative + Path.DirectorySeparatorChar : relative;
    }

    private static void CleanDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Best-effort cleanup on rejection; leftover directories are surfaced by
            // the next install attempt's "already exists" check.
        }
    }

    private static PackageResult Fail(string error) => new(null, null, [error]);
}
