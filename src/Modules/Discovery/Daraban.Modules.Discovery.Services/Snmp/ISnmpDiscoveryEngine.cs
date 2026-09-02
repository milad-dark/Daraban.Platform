using Daraban.Modules.Discovery.Data.Entities;

namespace Daraban.Modules.Discovery.Services.Snmp;

/// <summary>
/// SNMP-based network device discovery engine (Task 5.2).
/// Handles SNMP v1/v2c/v3 queries, OID walking, device fingerprinting,
/// and printer/switch/router-specific data collection.
/// </summary>
public interface ISnmpDiscoveryEngine
{
    /// <summary>
    /// Performs a quick SNMP probe (sysDescr, sysObjectID, sysName) to determine
    /// if a host responds to SNMP and identify its device type.
    /// </summary>
    Task<SnmpProbeResult?> ProbeAsync(
        string ipAddress,
        SnmpConnectionInfo connection,
        int timeoutMs = 2000,
        CancellationToken ct = default);

    /// <summary>
    /// Performs a full SNMP discovery walk on a device, collecting:
    /// System info (sysDescr, sysName, sysLocation, sysContact, sysUpTime)
    /// Interface table (ifDescr, ifPhysAddress, ifAdminStatus, ifOperStatus, ifSpeed)
    /// Host resources (hrSWInstalledTable, hrStorageTable)
    /// </summary>
    Task<SnmpDiscoveryResult?> DiscoverAsync(
        string ipAddress,
        SnmpConnectionInfo connection,
        int timeoutMs = 3000,
        CancellationToken ct = default);

    /// <summary>
    /// Collects printer-specific MIB data (page counts, toner levels, status).
    /// </summary>
    Task<PrinterMibData?> GetPrinterDataAsync(
        string ipAddress,
        SnmpConnectionInfo connection,
        int timeoutMs = 2000,
        CancellationToken ct = default);

    /// <summary>
    /// Collects switch/router port mapping from ifTable and enterprise OIDs.
    /// </summary>
    Task<PortMappingResult?> GetPortMappingAsync(
        string ipAddress,
        SnmpConnectionInfo connection,
        int timeoutMs = 3000,
        CancellationToken ct = default);

    /// <summary>
    /// Fingerprint device type from SNMP responses using sysDescr, sysObjectID,
    /// and enterprise OID patterns.
    /// </summary>
    string? FingerprintDeviceType(string? sysDescr, string? sysObjectId);
}

/// <summary>
/// Connection information for SNMP queries, derived from SnmpCredential entity.
/// Supports SNMPv1 and v2c. SNMPv3 support requires additional USM security
/// parameters and engine ID discovery (out of scope for Task 5.2).
/// </summary>
public class SnmpConnectionInfo
{
    public SnmpVersion Version { get; set; } = SnmpVersion.V2c;
    public string? CommunityString { get; set; }
    public string? UserName { get; set; }
    public string? AuthPassphrase { get; set; }
    public string? PrivPassphrase { get; set; }
    public int AuthProtocol { get; set; } = 0;  // Reserved for SNMPv3
    public int PrivProtocol { get; set; } = 0;   // Reserved for SNMPv3
}

/// <summary>
/// Result from a quick SNMP probe (3 OIDs).
/// </summary>
public class SnmpProbeResult
{
    public string? SysDescr { get; set; }
    public string? SysObjectId { get; set; }
    public string? SysName { get; set; }
    public string? DeviceType { get; set; }
    public bool IsSnmpReachable { get; set; }
}

/// <summary>
/// Full SNMP discovery result with system info, interfaces, and host resources.
/// </summary>
public class SnmpDiscoveryResult
{
    // System info
    public string? SysDescr { get; set; }
    public string? SysObjectId { get; set; }
    public string? SysName { get; set; }
    public string? SysLocation { get; set; }
    public string? SysContact { get; set; }
    public long? SysUpTime { get; set; }  // in timeticks (1/100s)

    // Device identification
    public string? DeviceType { get; set; }
    public string? Vendor { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? OsVersion { get; set; }

    // Interfaces
    public List<SnmpInterfaceInfo> Interfaces { get; set; } = new();

    // Storage (hrStorageTable)
    public List<StorageInfo> Storage { get; set; } = new();

    // Software (hrSWInstalledTable)
    public List<SoftwareInfo> InstalledSoftware { get; set; } = new();
}

/// <summary>
/// Network interface info from ifTable.
/// </summary>
public class SnmpInterfaceInfo
{
    public int Index { get; set; }
    public string? Description { get; set; }
    public string? MacAddress { get; set; }
    public int? Type { get; set; }
    public int? AdminStatus { get; set; }
    public int? OperStatus { get; set; }
    public long? Speed { get; set; }
    public string? IpAddress { get; set; }
}

/// <summary>
/// Storage info from hrStorageTable.
/// </summary>
public class StorageInfo
{
    public string? Description { get; set; }
    public long? Size { get; set; }
    public long? Used { get; set; }
    public string? Type { get; set; }
}

/// <summary>
/// Software info from hrSWInstalledTable.
/// </summary>
public class SoftwareInfo
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Vendor { get; set; }
    public int? Type { get; set; }
}

/// <summary>
/// Printer-specific MIB data.
/// </summary>
public class PrinterMibData
{
    // prtGeneral (RFC 3805)
    public int? PrinterStatus { get; set; }
    public int? PrinterErrorState { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }

    // prtMarkerSupplies (toner/ink levels)
    public List<TonerLevel> TonerLevels { get; set; } = new();

    // prtMarkerCounter (page counts)
    public long? TotalPagesPrinted { get; set; }
    public long? ColorPagesPrinted { get; set; }
    public long? MonoPagesPrinted { get; set; }

    // prtInput (paper trays)
    public List<PaperTrayInfo> PaperTrays { get; set; } = new();
}

/// <summary>
/// Toner/ink level info.
/// </summary>
public class TonerLevel
{
    public string? Color { get; set; }
    public int? Level { get; set; }        // 0-100 or -1 (unknown)
    public string? MaxCapacity { get; set; }
    public string? CurrentLevel { get; set; }
}

/// <summary>
/// Paper tray info.
/// </summary>
public class PaperTrayInfo
{
    public int? Index { get; set; }
    public string? Type { get; set; }
    public int? CurrentLevel { get; set; }
    public int? MaxCapacity { get; set; }
}

/// <summary>
/// Switch/router port mapping result.
/// </summary>
public class PortMappingResult
{
    public List<PortInfo> Ports { get; set; } = new();
    public int? TotalPorts { get; set; }
    public int? ActivePorts { get; set; }
    public int? DownPorts { get; set; }
}

/// <summary>
/// Individual port info from ifTable + enterprise OIDs.
/// </summary>
public class PortInfo
{
    public int? IfIndex { get; set; }
    public string? IfName { get; set; }
    public string? IfDescription { get; set; }
    public int? IfType { get; set; }
    public int? AdminStatus { get; set; }
    public int? OperStatus { get; set; }
    public long? Speed { get; set; }
    public string? MacAddress { get; set; }
    public string? VlanId { get; set; }
    public string? ConnectedDeviceMac { get; set; }
}
