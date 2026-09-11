using System.Text;

namespace Daraban.Modules.Settings.Data;

/// <summary>Type of the value a setting holds; drives validation and the frontend editor.</summary>
public enum SettingValueType
{
    String,
    Int,
    Boolean,
    /// <summary>Local time-of-day (e.g. working-hours bounds), always encoded "HH:mm".</summary>
    Time,
}

/// <summary>Top-level settings groups -- the tabs of the settings UI and the routing key
/// for connectivity tests (Task 7.4).</summary>
public static class SettingCategories
{
    public const string Email = "email";
    public const string Ldap = "ldap";
    public const string Branding = "branding";
    public const string Security = "security";
    public const string Time = "time";

    /// <summary>All valid categories, in UI tab order.</summary>
    public static readonly string[] All = [Email, Ldap, Branding, Security, Time];
}

/// <summary>
/// The closed catalog of known settings (Task 7.4): key, value type, category, default and
/// description. The database is pre-seeded from this list at startup, so the settings table
/// always contains exactly these rows -- the API exposes update-only semantics and unknown
/// keys are rejected before they can reach the database.
/// </summary>
/// <remarks>
/// To add a setting: append one entry here. The seeder inserts it on the next startup;
/// existing rows are never touched, so operator-modified values survive upgrades.
/// </remarks>
public static class SettingCatalog
{
    public sealed record Definition(
        string Key,
        string Default,
        SettingValueType Type,
        string Category,
        string Description,
        bool IsSecret);

    public static readonly Definition[] All =
    [
        // ---- Email (SMTP) --------------------------------------------------------
        new("email.smtp_host", "", SettingValueType.String, SettingCategories.Email,
            "SMTP server hostname or IP used to send platform email.", IsSecret: false),
        new("email.smtp_port", "587", SettingValueType.Int, SettingCategories.Email,
            "SMTP server port (587 submission, 465 implicit TLS, 25 plain).", IsSecret: false),
        new("email.smtp_user", "", SettingValueType.String, SettingCategories.Email,
            "SMTP username for authenticated submission.", IsSecret: false),
        new("email.smtp_password", "", SettingValueType.String, SettingCategories.Email,
            "SMTP password. Stored in the database; always masked in the API.", IsSecret: true),
        new("email.smtp_tls", "starttls", SettingValueType.String, SettingCategories.Email,
            "TLS mode: none, starttls or ssl.", IsSecret: false),
        new("email.from_address", "no-reply@daraban.local", SettingValueType.String, SettingCategories.Email,
            "From address used on outgoing platform email.", IsSecret: false),

        // ---- LDAP ---------------------------------------------------------------
        new("ldap.server", "", SettingValueType.String, SettingCategories.Ldap,
            "LDAP server host (ldap:// or ldaps:// URL, or bare hostname with the port below).", IsSecret: false),
        new("ldap.port", "389", SettingValueType.Int, SettingCategories.Ldap,
            "LDAP port (389 for ldap/start-TLS, 636 for LDAPS).", IsSecret: false),
        new("ldap.base_dn", "", SettingValueType.String, SettingCategories.Ldap,
            "Base DN all directory searches are rooted at.", IsSecret: false),
        new("ldap.bind_user", "", SettingValueType.String, SettingCategories.Ldap,
            "Bind DN (or user@domain) used for directory access.", IsSecret: false),
        new("ldap.bind_password", "", SettingValueType.String, SettingCategories.Ldap,
            "Bind password. Stored in the database; always masked in the API.", IsSecret: true),
        new("ldap.attribute_username", "uid", SettingValueType.String, SettingCategories.Ldap,
            "Directory attribute mapped to the platform username.", IsSecret: false),
        new("ldap.attribute_email", "mail", SettingValueType.String, SettingCategories.Ldap,
            "Directory attribute mapped to the platform email address.", IsSecret: false),
        new("ldap.attribute_display_name", "cn", SettingValueType.String, SettingCategories.Ldap,
            "Directory attribute mapped to the platform display name.", IsSecret: false),
        new("ldap.sync_schedule", "0 */6 * * *", SettingValueType.String, SettingCategories.Ldap,
            "Directory sync schedule in cron format (default: every 6 hours).", IsSecret: false),

        // ---- Branding -------------------------------------------------------------
        new("branding.application_name", "Daraban Platform", SettingValueType.String, SettingCategories.Branding,
            "Application name shown in the UI header, titles and emails.", IsSecret: false),
        new("branding.logo_url", "", SettingValueType.String, SettingCategories.Branding,
            "URL of the logo image. Use https:// URLs only; browsers block mixed content.", IsSecret: false),
        new("branding.primary_color", "#1976d2", SettingValueType.String, SettingCategories.Branding,
            "Primary UI color as a hex triplet (#rrggbb).", IsSecret: false),

        // ---- Security ---------------------------------------------------------------
        new("security.session_timeout_minutes", "480", SettingValueType.Int, SettingCategories.Security,
            "Browser session lifetime in minutes; the refresh token is capped to this.", IsSecret: false),
        new("security.max_login_attempts", "5", SettingValueType.Int, SettingCategories.Security,
            "Failed logins allowed before the account is temporarily locked.", IsSecret: false),
        new("security.mfa_enforced", "false", SettingValueType.Boolean, SettingCategories.Security,
            "Require MFA for all users. Reserved: MFA enrollment lands with the Notifications module.", IsSecret: false),

        // ---- Time -------------------------------------------------------------------
        new("time.default_timezone", "UTC", SettingValueType.String, SettingCategories.Time,
            "Default IANA timezone for displaying and scheduling (e.g. UTC, Europe/Berlin, Asia/Tehran).", IsSecret: false),
        new("time.date_format", "yyyy-MM-dd", SettingValueType.String, SettingCategories.Time,
            "Default date display format (e.g. yyyy-MM-dd, dd/MM/yyyy, MM/dd/yyyy).", IsSecret: false),
        new("time.work_start", "08:00", SettingValueType.Time, SettingCategories.Time,
            "Start of the working day in local time; used for SLA and scheduling windows.", IsSecret: false),
        new("time.work_end", "17:00", SettingValueType.Time, SettingCategories.Time,
            "End of the working day in local time; used for SLA and scheduling windows.", IsSecret: false),
    ];

    /// <summary>Case-sensitive lookup by key; null when the key is not in the catalog.</summary>
    public static Definition? Find(string key) =>
        Array.Find(All, d => d.Key == key);

    /// <summary>
    /// Builds one INSERT that seeds every catalog row with ON CONFLICT DO NOTHING, so
    /// operator-modified values are never overwritten on re-run. This ships as SQL rather
    /// than an EF migration because this module (like every module except Knowledge) does
    /// not carry a migration assembly yet -- the SQL is idempotent and safe to run on every
    /// startup, and will become a proper migration the moment this module gets one.
    /// </summary>
    public static string BuildSeedSql()
    {
        var sb = new StringBuilder(4096);
        sb.AppendLine("CREATE TABLE IF NOT EXISTS core.system_settings (");
        sb.AppendLine("    id uuid PRIMARY KEY,");
        sb.AppendLine("    key character varying(100) NOT NULL,");
        sb.AppendLine("    value text NOT NULL,");
        sb.AppendLine("    value_type character varying(20) NOT NULL,");
        sb.AppendLine("    category character varying(30) NOT NULL,");
        sb.AppendLine("    description character varying(500) NOT NULL,");
        sb.AppendLine("    is_secret boolean NOT NULL,");
        sb.AppendLine("    created_at timestamp with time zone NOT NULL,");
        sb.AppendLine("    updated_at timestamp with time zone NOT NULL,");
        sb.AppendLine("    created_by_id uuid,");
        sb.AppendLine("    updated_by_id uuid);");
        sb.AppendLine("CREATE UNIQUE INDEX IF NOT EXISTS uq_system_settings_key ON core.system_settings (key);");
        sb.AppendLine("CREATE INDEX IF NOT EXISTS ix_system_settings_category ON core.system_settings (category);");
        sb.AppendLine("INSERT INTO core.system_settings (id, key, value, value_type, category, description, is_secret, created_at, updated_at)");
        sb.AppendLine("SELECT v.id, v.key, v.value, v.value_type, v.category, v.description, v.is_secret, now(), now()");
        sb.AppendLine("FROM (VALUES");
        for (var i = 0; i < All.Length; i++)
        {
            var d = All[i];
            var comma = i < All.Length - 1 ? "," : "";
            sb.Append("    ('").Append(Guid.NewGuid()).Append("', '")
                .Append(d.Key).Append("', '")
                .Append(d.Default.Replace("'", "''")).Append("', '")
                .Append(d.Type.ToString().ToLowerInvariant()).Append("', '")
                .Append(d.Category).Append("', '")
                .Append(d.Description.Replace("'", "''")).Append("', ")
                .Append(d.IsSecret ? "true" : "false").Append(')')
                .AppendLine(comma);
        }
        sb.AppendLine(") AS v(id, key, value, value_type, category, description, is_secret)");
        // key is the join column; PK id differs on conflict and must not trigger a match.
        sb.AppendLine("ON CONFLICT (key) DO NOTHING;");
        return sb.ToString();
    }
}
