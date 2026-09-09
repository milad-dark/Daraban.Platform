using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daraban.Modules.Reporting.Services.Storage;

/// <summary>Options for the report file store (Task 7.2), bound to the "ReportStore" config
/// section. RootPath may be relative (resolved against the content root) or absolute; in the
/// Docker deployment it should point at a mounted volume.</summary>
public sealed class ReportStoreOptions
{
    public const string SectionName = "ReportStore";

    /// <summary>Store root directory. Default keeps reports out of the app tree.</summary>
    public string RootPath { get; set; } = "/var/daraban/reports";

    /// <summary>Hard cap on a single artifact's size -- a runaway renderer must not fill the
    /// volume. 0 disables the cap.</summary>
    public long MaxFileBytes { get; set; } = 256 * 1024 * 1024;
}

/// <summary>
/// Abstraction over where generated report artifacts live (Task 7.2). References are
/// store-relative, never absolute paths -- the DB stores a reference, not a location, so the
/// store's layout can change without touching data.
/// </summary>
public interface IReportFileStore
{
    /// <summary>Writes the artifact and returns its store-relative reference.</summary>
    Task<string> WriteAsync(Guid savedReportId, string format, byte[] content, CancellationToken ct = default);

    /// <summary>Opens the artifact for reading, or null when the reference does not resolve.</summary>
    Task<Stream?> OpenReadAsync(string reference, CancellationToken ct = default);

    /// <summary>Best-effort existence check (used for diagnostics on download).</summary>
    Task<bool> ExistsAsync(string reference, CancellationToken ct = default);
}

/// <summary>Local/attached-directory implementation (SavedReportStorageKind.FileSystem).</summary>
public sealed class LocalReportFileStore : IReportFileStore
{
    private readonly string _root;
    private readonly long _maxFileBytes;
    private readonly ILogger<LocalReportFileStore> _logger;

    public LocalReportFileStore(IOptions<ReportStoreOptions> options, IHostEnvironment env, ILogger<LocalReportFileStore> logger)
    {
        var root = options.Value.RootPath;
        _root = Path.IsPathRooted(root) ? root : Path.GetFullPath(Path.Combine(env.ContentRootPath, root));
        _maxFileBytes = options.Value.MaxFileBytes;
        _logger = logger;

        Directory.CreateDirectory(_root);
    }

    public async Task<string> WriteAsync(Guid savedReportId, string format, byte[] content, CancellationToken ct = default)
    {
        if (_maxFileBytes > 0 && content.LongLength > _maxFileBytes)
            throw new InvalidOperationException(
                $"Report artifact size {content.LongLength} exceeds the configured cap {_maxFileBytes}.");

        // Date-bucketed relative path: sharding keeps any single directory small, and the
        // reference doubles as a human-readable audit trail in the store.
        var relative = $"{DateTimeOffset.UtcNow:yyyy/MM/dd}/{savedReportId:N}.{format.ToLowerInvariant()}";
        var fullPath = Resolve(relative);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, ct);

        _logger.LogInformation("Report artifact written: {Reference} ({Bytes} bytes)", relative, content.LongLength);
        return relative;
    }

    public Task<Stream?> OpenReadAsync(string reference, CancellationToken ct = default)
    {
        var fullPath = Resolve(reference);
        return Task.FromResult<Stream?>(File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            : null);
    }

    public Task<bool> ExistsAsync(string reference, CancellationToken ct = default) =>
        Task.FromResult(File.Exists(Resolve(reference)));

    /// <summary>Reference -> physical path. The store validates that the reference stays inside
    /// the root (defense against a tampered storage_reference in the DB escaping the store).</summary>
    private string Resolve(string reference)
    {
        var fullRoot = Path.GetFullPath(_root);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, reference));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !fullPath.Equals(fullRoot, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException($"Report reference '{reference}' escapes the report store root.");
        }
        return fullPath;
    }
}
