namespace Daraban.Modules.Discovery.Data.Entities;

/// <summary>
/// Defines an IP range to be scanned for device discovery (Task 5.1).
/// Contains the CIDR notation, scan type, and optional credential reference.
/// </summary>
public class DiscoveryRange
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name for this range (e.g., "Office Floor 1").</summary>
    public string Name { get; set; } = default!;

    /// <summary>CIDR notation (e.g., "192.168.1.0/24") or start-end range.</summary>
    public string CidrRange { get; set; } = default!;

    /// <summary>Optional start IP for explicit range notation.</summary>
    public string? StartIp { get; set; }

    /// <summary>Optional end IP for explicit range notation.</summary>
    public string? EndIp { get; set; }

    /// <summary>Scan type: Ping, Snmp, Wmi, Ssh, Http.</summary>
    public ScanType ScanType { get; set; } = ScanType.Ping;

    /// <summary>Optional SNMP credential for SNMP scans.</summary>
    public Guid? SnmpCredentialId { get; set; }

    /// <summary>Whether this range is active for scanning.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>How often to scan (in hours). 0 = manual only.</summary>
    public int ScanIntervalHours { get; set; } = 0;

    /// <summary>Last time this range was scanned (null if never).</summary>
    public DateTimeOffset? LastScanAt { get; set; }

    /// <summary>When this range was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this range was last modified.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>User who created this range.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Navigation: associated SNMP credential.</summary>
    public SnmpCredential? SnmpCredential { get; set; }

    /// <summary>Navigation: scans performed on this range.</summary>
    public ICollection<DiscoveryScan> Scans { get; set; } = new List<DiscoveryScan>();
}

/// <summary>Scan type enumeration.</summary>
public enum ScanType
{
    /// <summary>ICMP ping sweep only.</summary>
    Ping = 0,

    /// <summary>SNMP query for device details.</summary>
    Snmp = 1,

    /// <summary>WMI for Windows devices.</summary>
    Wmi = 2,

    /// <summary>SSH for Linux/Unix devices.</summary>
    Ssh = 3,

    /// <summary>HTTP/HTTPS probe.</summary>
    Http = 4,

    /// <summary>Combined scan (ping + SNMP + port probe).</summary>
    Combined = 5,
}
