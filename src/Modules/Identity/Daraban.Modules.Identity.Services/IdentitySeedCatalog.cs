namespace Daraban.Modules.Identity.Services;

/// <summary>
/// The seed data's single source of truth: profile names, seed usernames, and the
/// permission catalog. The catalog mirrors the [RequirePermission("module.action")]
/// strings on the controllers (see the task write-up for the extraction command) —
/// when a module adds a new permission, add it here so the seeded Super-Admin keeps
/// working. Guarded by a unit test so drift between the two lists is caught in CI.
/// </summary>
public static class IdentitySeedCatalog
{
    public const string RootEntityName = "/Daraban/";
    public const string AdminProfileName = "Super-Admin";
    public const string UserProfileName = "Standard User";
    public const string AdminUsername = "admin";
    public const string UserUsername = "user";

    /// <summary>Every permission string the API surface checks via [RequirePermission].</summary>
    public static readonly IReadOnlySet<string> AllPermissions = new HashSet<string>
    {
        // Identity (Task 2.4 — the first dynamic-permission consumer)
        "identity.users.read", "identity.users.write", "identity.users.delete",
        "identity.auditlogs.read",

        // Assets
        "assets.read", "assets.write", "assets.delete",

        // ServiceDesk
        "servicedesk.read", "servicedesk.write", "servicedesk.delete",

        // Financial
        "financial.read", "financial.write", "financial.delete",

        // Knowledge
        "knowledge.read", "knowledge.write", "knowledge.publish", "knowledge.delete",

        // Software
        "software.read", "software.write", "software.delete",

        // Discovery
        "discovery.read", "discovery.write", "discovery.delete",

        // Reporting
        "reports.read", "reports.manage",

        // Dashboard
        "dashboard.read", "dashboard.write",

        // Settings / Plugins
        "settings.read", "settings.write",
        "plugins.read", "plugins.write",
    };

    /// <summary>The read-only working set for the Standard User profile: everything the
    /// operational modules' list/detail screens need, plus dashboard.write (per-user widget
    /// layout) — but no write/delete/publish/manage rights anywhere.</summary>
    public static readonly IReadOnlySet<string> UserPermissions = new HashSet<string>
    {
        "identity.users.read",
        "assets.read",
        "servicedesk.read", "servicedesk.write",
        "knowledge.read",
        "software.read",
        "discovery.read",
        "reports.read",
        "dashboard.read", "dashboard.write",
        "settings.read",
    };
}
