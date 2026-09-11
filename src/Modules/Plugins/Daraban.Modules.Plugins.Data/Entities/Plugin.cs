using Daraban.Platform.Common;

namespace Daraban.Modules.Plugins.Data.Entities;

/// <summary>
/// Registry row for one installed plugin package (Task 7.5). Written by the plugin
/// manager during install/uninstall lifecycle transitions; read by the API and the
/// startup loader. The full manifest is preserved verbatim in <see cref="ManifestJson"/>
/// so re-enabling and future upgrades never need to re-parse a package.
/// </summary>
/// <remarks>
/// Lives in the cross-cutting <c>core</c> schema next to system_settings and audit_logs:
/// the plugin registry is platform infrastructure, not any single module's private data.
/// Lifecycle states and their transitions are documented on <see cref="PluginStatus"/>
/// in the Plugins abstractions assembly.
/// </remarks>
public class Plugin : BaseEntity
{
    /// <summary>Plugin identity, e.g. "sample-asset-tagger" -- matches the package manifest id.</summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>Display name from the manifest (max 100 chars, validated before insert).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Manifest version string (semver, validated before insert).</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Plugin type: "asset" | "ticket-automation" | "report" | "integration".</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Lifecycle status: installed, enabled, disabled, uninstalled (tombstone).</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>UTC instant the package was installed.</summary>
    public DateTimeOffset InstalledAt { get; set; }

    /// <summary>Full manifest.json content, preserved verbatim (validated on load).</summary>
    public string ManifestJson { get; set; } = string.Empty;
}
