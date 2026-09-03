using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Data.Entities;

/// <summary>
/// Software installation entity — tracks software installed on assets.
/// Links software licenses to specific devices.
/// </summary>
public class SoftwareInstallation
{
    /// <summary>Installation ID.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Associated software.</summary>
    public Guid SoftwareId { get; set; }
    public Software Software { get; set; } = null!;

    /// <summary>Associated license (optional).</summary>
    public Guid? LicenseId { get; set; }
    public SoftwareLicense? License { get; set; }

    /// <summary>Associated asset (device).</summary>
    public Guid AssetId { get; set; }

    /// <summary>Installed version.</summary>
    public string? InstalledVersion { get; set; }

    /// <summary>Installation date.</summary>
    public DateTimeOffset InstalledDate { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Uninstallation date (null if still installed).</summary>
    public DateTimeOffset? UninstalledDate { get; set; }

    /// <summary>Is this installation currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Installation path (if known).</summary>
    public string? InstallPath { get; set; }

    /// <summary>Installation source (manual, agent, SCCM, etc.).</summary>
    public InstallationSource Source { get; set; } = InstallationSource.Manual;

    /// <summary>Comments about the installation.</summary>
    public string? Comment { get; set; }

    /// <summary>Created timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Updated timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Whether this installation has been deleted.</summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>Deleted timestamp.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}

public enum InstallationSource
{
    /// <summary>Manually installed by user.</summary>
    Manual = 1,

    /// <summary>Installed by agent (GLPI Agent, etc.).</summary>
    Agent = 2,

    /// <summary>Installed via SCCM/MECM.</summary>
    SCCM = 3,

    /// <summary>Installed via Intune.</summary>
    Intune = 4,

    /// <summary>Installed via script.</summary>
    Script = 5,

    /// <summary>Installed via package manager.</summary>
    PackageManager = 6,

    /// <summary>Installation source unknown.</summary>
    Unknown = 7
}
