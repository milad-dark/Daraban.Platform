using System.Text.RegularExpressions;

namespace Daraban.Platform.Plugins;

/// <summary>
/// Plugin lifecycle status persisted in <c>core.plugins</c> and used by the manager
/// to decide which transitions are legal. Transitions: Installing -> Installed ->
/// Enabled, and any active state -> Disabled -> Uninstalled.
/// </summary>
public enum PluginStatus
{
    /// <summary>Package extracted and validated, but not yet activated.</summary>
    Installed = 1,

    /// <summary>Loaded, services registered, migrations applied; visible to users.</summary>
    Enabled = 2,

    /// <summary>Deactivated: assembly unloaded, but files and schema kept for re-enable.</summary>
    Disabled = 3,

    /// <summary>Files and plugin schema dropped; only a tombstone row remains.</summary>
    Uninstalled = 4,
}

/// <summary>
/// The <c>manifest.json</c> file that must sit at the root of every plugin package.
/// The host validates every field <em>before</em> any assembly is loaded -- a malformed
/// manifest is the cheapest possible failure.
/// </summary>
public sealed partial class PluginManifest
{
    /// <summary>Plugin identity pattern: lowercase letters, digits, dashes. Used verbatim in
    /// the plugin's directory name and DB schema name (<c>plugins_{id}</c>), so it must
    /// never contain path separators, whitespace, or uppercase.</summary>
    public const string IdPattern = "^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$";

    /// <summary>Semver pattern (major.minor.patch with optional pre-release/build).</summary>
    public const string VersionPattern = @"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$";

    /// <summary>Dot-separated type name of the <c>IPlugin</c> implementation.</summary>
    public const string EntryPointPattern = @"^[A-Za-z_][A-Za-z0-9_.]{0,250}[A-Za-z0-9]$";

    /// <summary>The plugin's declared plugin-type; see <see cref="PluginTypes"/>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Unique id matching <see cref="IdPattern"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display name (1..100 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Semver string matching <see cref="VersionPattern"/>.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Dot-separated <c>IPlugin</c> implementation type name.</summary>
    public string EntryPoint { get; set; } = string.Empty;

    /// <summary>Optional author shown in the plugin manager UI.</summary>
    public string? Author { get; set; }

    /// <summary>Optional short description shown in the plugin manager UI.</summary>
    public string? Description { get; set; }

    /// <summary>Minimum host version this plugin supports; unused versions pass.</summary>
    public string? MinimumHostVersion { get; set; }

    /// <summary>Optional URL of the plugin's custom UI (Angular element / federated module).</summary>
    public string? RemoteEntryUrl { get; set; }

    /// <summary>
    /// The exact plugin assembly filename inside the package, e.g. <c>Sample.Plugin.dll</c>.
    /// Validated against traversal characters before it is ever combined into a path.
    /// </summary>
    public string AssemblyFile { get; set; } = string.Empty;

    /// <summary>Validates every field and returns the list of violations (empty when valid).</summary>
    public List<string> Validate() => Validate(PluginTypes.Allowed);

    /// <summary>Validates a fully-loaded manifest against an exact set of allowed types.</summary>
    public List<string> Validate(IReadOnlyCollection<string> allowedTypes)
    {
        var errors = new List<string>();

        if (!allowedTypes.Contains(Type))
            errors.Add($"manifest.type '{Type}' is not one of: {string.Join(", ", allowedTypes)}.");
        if (!IdRegex().IsMatch(Id))
            errors.Add($"manifest.id '{Id}' must match {IdPattern}.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100)
            errors.Add("manifest.name is required (max 100 chars).");
        if (!VersionRegex().IsMatch(Version))
            errors.Add($"manifest.version '{Version}' must be semver.");
        if (!EntryPointRegex().IsMatch(EntryPoint))
            errors.Add($"manifest.entryPoint '{EntryPoint}' must be a plain type name.");
        if (!AssemblyFileRegex().IsMatch(AssemblyFile))
            errors.Add($"manifest.assemblyFile '{AssemblyFile}' must be a plain file name.");
        if (RemoteEntryUrl is { Length: > 0 }
            && (!Uri.TryCreate(RemoteEntryUrl, UriKind.Absolute, out var remoteUri)
                || (remoteUri.Scheme != Uri.UriSchemeHttp && remoteUri.Scheme != Uri.UriSchemeHttps)))
            errors.Add($"manifest.remoteEntryUrl '{RemoteEntryUrl}' must be an absolute http(s) URL.");

        return errors;
    }

    [GeneratedRegex(IdPattern)]
    private partial System.Text.RegularExpressions.Regex IdRegex();

    [GeneratedRegex(VersionPattern)]
    private partial System.Text.RegularExpressions.Regex VersionRegex();

    [GeneratedRegex(EntryPointPattern)]
    private partial System.Text.RegularExpressions.Regex EntryPointRegex();

    [GeneratedRegex("^[A-Za-z0-9_.-]{1,180}\\.dll$")]
    private partial System.Text.RegularExpressions.Regex AssemblyFileRegex();
}

/// <summary>The four plugin types supported by the platform (Task 7.5).</summary>
public static class PluginTypes
{
    public const string Asset = "asset";
    public const string TicketAutomation = "ticket-automation";
    public const string Report = "report";
    public const string Integration = "integration";

    /// <summary>All valid <c>manifest.type</c> values.</summary>
    public static readonly string[] All = [Asset, TicketAutomation, Report, Integration];

    /// <summary>Helper for validating deserialized manifests.</summary>
    public static readonly IReadOnlyCollection<string> Allowed = All;
}
