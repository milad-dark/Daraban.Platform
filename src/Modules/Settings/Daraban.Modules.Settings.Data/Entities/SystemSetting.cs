using Daraban.Platform.Common;

namespace Daraban.Modules.Settings.Data.Entities;

/// <summary>
/// One platform-wide configuration value (Task 7.4). Rows are pre-seeded from
/// <see cref="SettingCatalog"/> at startup, so the API only ever reads and updates keys
/// the catalog knows about -- there is no create/delete surface, by design.
/// </summary>
/// <remarks>
/// Secret values are stored in plain text in this release: per-row encryption needs key
/// management this platform does not have yet (no KMS/DPAPI story, see docs/06). The
/// compensating controls are: secrets never leave the API except masked, the audit
/// interceptor redacts values it serializes, and log statements never include values.
/// </remarks>
public class SystemSetting : BaseEntity
{
    /// <summary>Dotted key, e.g. "email.smtp_host". Unique; must match <see cref="SettingCatalog"/> exactly.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Raw string-encoded value; interpretation is defined by <see cref="ValueType"/>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>"string" | "int" | "boolean" | "time" -- mirrors SettingValueType for the frontend editor.</summary>
    public string ValueType { get; set; } = string.Empty;

    /// <summary>"email" | "ldap" | "branding" | "security" | "time" -- groups the tabbed UI and routes connectivity tests.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Human-readable explanation shown in the settings UI.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>True when the value is a credential -- always masked in API responses.</summary>
    public bool IsSecret { get; set; }
}
