namespace Daraban.Modules.Discovery.Data.Entities;

/// <summary>
/// Represents a device discovered during a network scan (Task 5.1).
/// Contains network identification, OS guess, and open ports.
/// </summary>
public class DiscoveredDevice
{
    /// <summary>Primary key.</summary>
    public long Id { get; set; }

    /// <summary>The scan that discovered this device.</summary>
    public Guid ScanId { get; set; }

    /// <summary>The range this device belongs to.</summary>
    public Guid RangeId { get; set; }

    /// <summary>IP address of the discovered device.</summary>
    public string IpAddress { get; set; } = default!;

    /// <summary>MAC address (if obtained via ARP/SNMP).</summary>
    public string? MacAddress { get; set; }

    /// <summary>Hostname (from DNS, SNMP, or reverse lookup).</summary>
    public string? Hostname { get; set; }

    /// <summary>Operating system guess (from SNMP, TTL, or banner grabbing).</summary>
    public string? OsGuess { get; set; }

    /// <summary>OS version if available.</summary>
    public string? OsVersion { get; set; }

    /// <summary>Device vendor/manufacturer (from OUI lookup).</summary>
    public string? Vendor { get; set; }

    /// <summary>Device model (from SNMP sysDescr).</summary>
    public string? Model { get; set; }

    /// <summary>Serial number (from SNMP).</summary>
    public string? SerialNumber { get; set; }

    /// <summary>Open ports as JSON array (e.g., [22, 80, 443, 3389]).</summary>
    public string? OpenPorts { get; set; }

    /// <summary>SNMP sysDescr response.</summary>
    public string? SysDescr { get; set; }

    /// <summary>SNMP sysName response.</summary>
    public string? SysName { get; set; }

    /// <summary>SNMP sysLocation response.</summary>
    public string? SysLocation { get; set; }

    /// <summary>SNMP sysContact response.</summary>
    public string? SysContact { get; set; }

    /// <summary>SNMP uptime in timeticks.</summary>
    public long? SnmpUptime { get; set; }

    /// <summary>Ping response time in milliseconds.</summary>
    public int? PingMs { get; set; }

    /// <summary>TTL value (helps identify OS).</summary>
    public int? Ttl { get; set; }

    /// <summary>Whether this device was successfully added as an Asset.</summary>
    public bool AssetCreated { get; set; } = false;

    /// <summary>The Asset ID if created (null until created).</summary>
    public Guid? AssetId { get; set; }

    /// <summary>When this device was discovered.</summary>
    public DateTimeOffset DiscoveredAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this device record was last updated (re-scan).</summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    /// <summary>Navigation: the scan that discovered this device.</summary>
    public DiscoveryScan Scan { get; set; } = default!;

    /// <summary>Navigation: the range this device belongs to.</summary>
    public DiscoveryRange Range { get; set; } = default!;
}
