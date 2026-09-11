namespace Daraban.Platform.Plugins;

/// <summary>
/// Host-side configuration for the plugin subsystem, bound from the <c>Plugins</c>
/// section of appsettings.json. Everything security-relevant is off by default except
/// signature validation which is opt-in (Task 7.5: "optional, configurable").
/// </summary>
public sealed class PluginsOptions
{
    public const string SectionName = "Plugins";

    /// <summary>Root directory plugin packages are extracted into. Default: <c>plugins</c> under the host.</summary>
    public string RootDirectory { get; set; } = "plugins";

    /// <summary>Maximum accepted plugin package size (default 20 MB).</summary>
    public int MaxPackageSizeBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>Maximum number of entries (files) a plugin package may contain (default 200).</summary>
    public int MaxPackageEntries { get; set; } = 200;

    /// <summary>Maximum total uncompressed size of a package (default 100 MB).</summary>
    public int MaxUncompressedBytes { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// When true, every plugin DLL must carry an Authenticode/X509 signature whose
    /// certificate chains to a trusted root, else installation is refused. Optional
    /// code-signing validation per Task 7.5.
    /// </summary>
    public bool RequireSignedAssemblies { get; set; }

    /// <summary>
    /// Optional path (PEM/DER or PFX) of a specific signing certificate plugins must be
    /// signed with. When empty, any certificate chaining to a trusted root is accepted.
    /// </summary>
    public string? RequiredSignerCertificatePath { get; set; }
}
