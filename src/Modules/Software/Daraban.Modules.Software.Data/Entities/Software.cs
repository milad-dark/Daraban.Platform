using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Data.Entities;

/// <summary>
/// Software entity — represents a software product in the catalog.
/// Tracks software details, versions, and license information.
/// </summary>
public class Software : TenantScopedEntity
{
    /// <summary>Software name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Software version.</summary>
    public string? Version { get; set; }

    /// <summary>Software editor/manufacturer.</summary>
    public string? Editor { get; set; }

    /// <summary>Software description.</summary>
    public string? Description { get; set; }

    /// <summary>Software category (e.g., Operating System, Office Suite, Antivirus).</summary>
    public SoftwareCategory Category { get; set; } = SoftwareCategory.Other;

    /// <summary>Software edition (e.g., Home, Pro, Enterprise).</summary>
    public string? Edition { get; set; }

    /// <summary>Is this software currently active/supported.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Is this software open source.</summary>
    public bool IsOpenSource { get; set; } = false;

    /// <summary>Is this software free (no license required).</summary>
    public bool IsFree { get; set; } = false;

    /// <summary>Software website URL.</summary>
    public string? Website { get; set; }

    /// <summary>Software documentation URL.</summary>
    public string? DocumentationUrl { get; set; }

    /// <summary>Comments about the software.</summary>
    public string? Comment { get; set; }

    // Navigation properties
    public ICollection<SoftwareLicense> Licenses { get; set; } = new List<SoftwareLicense>();
    public ICollection<SoftwareInstallation> Installations { get; set; } = new List<SoftwareInstallation>();
}

public enum SoftwareCategory
{
    OperatingSystem = 1,
    OfficeSuite = 2,
    WebBrowser = 3,
    Antivirus = 4,
    DevelopmentTools = 5,
    Database = 6,
    Communication = 7,
    Graphics = 8,
    Security = 9,
    Utility = 10,
    Other = 11
}
