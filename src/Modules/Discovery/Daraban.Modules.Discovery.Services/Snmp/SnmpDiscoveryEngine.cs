using Daraban.Modules.Discovery.Data.Entities;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Daraban.Modules.Discovery.Services.Snmp;

/// <summary>
/// SNMP-based network device discovery engine (Task 5.2).
/// Implements SNMP v1/v2c queries, OID walking, device fingerprinting,
/// and printer/switch/router-specific data collection.
/// Uses Lextm.SharpSnmpLib (same library as Daraban.Agent.Core).
/// NOTE: SNMPv3 support requires engine ID discovery and USM security parameters.
///       Current implementation supports v1/v2c. v3 support can be added by
///       implementing GetDiscoveryParameters() for engine ID retrieval.
/// </summary>
public class SnmpDiscoveryEngine(ILogger<SnmpDiscoveryEngine> logger) : ISnmpDiscoveryEngine
{
    // ── Standard RFC OIDs ──────────────────────────────────────────────────
    private static readonly string OidSysDescr = "1.3.6.1.2.1.1.1.0";
    private static readonly string OidSysObjectID = "1.3.6.1.2.1.1.2.0";
    private static readonly string OidSysUpTime = "1.3.6.1.2.1.1.3.0";
    private static readonly string OidSysContact = "1.3.6.1.2.1.1.4.0";
    private static readonly string OidSysName = "1.3.6.1.2.1.1.5.0";
    private static readonly string OidSysLocation = "1.3.6.1.2.1.1.6.0";

    // ifTable OIDs
    private static readonly string OidIfDescr = "1.3.6.1.2.1.2.2.1.2";
    private static readonly string OidIfType = "1.3.6.1.2.1.2.2.1.3";
    private static readonly string OidIfSpeed = "1.3.6.1.2.1.2.2.1.5";
    private static readonly string OidIfPhysAddress = "1.3.6.1.2.1.2.2.1.6";
    private static readonly string OidIfAdminStatus = "1.3.6.1.2.1.2.2.1.7";
    private static readonly string OidIfOperStatus = "1.3.6.1.2.1.2.2.1.8";

    // Host Resources - Storage
    private static readonly string OidHrStorageDescr = "1.3.6.1.2.1.25.2.3.1.3";
    private static readonly string OidHrStorageSize = "1.3.6.1.2.1.25.2.3.1.5";
    private static readonly string OidHrStorageUsed = "1.3.6.1.2.1.25.2.3.1.6";
    private static readonly string OidHrStorageType = "1.3.6.1.2.1.25.2.3.1.2";

    // Host Resources - Software
    private static readonly string OidHrSWInstalledName = "1.3.6.1.2.1.25.3.2.1.3";
    private static readonly string OidHrSWInstalledVer = "1.3.6.1.2.1.25.3.2.1.5";
    private static readonly string OidHrSWInstalledVendor = "1.3.6.1.2.1.25.3.2.1.4";
    private static readonly string OidHrSWInstalledType = "1.3.6.1.2.1.25.3.2.1.2";

    // Printer MIB (RFC 3805)
    private static readonly string OidPrtGeneralStatus = "1.3.6.1.2.1.43.5.1.1.1";
    private static readonly string OidPrtGeneralErrorState = "1.3.6.1.2.1.43.5.1.1.2";
    private static readonly string OidPrtGeneralSerialNum = "1.3.6.1.2.1.43.5.1.1.17";
    private static readonly string OidPrtGeneralFirmware = "1.3.6.1.2.1.43.5.1.1.18";

    // Printer marker supplies (toner)
    private static readonly string OidPrtMarkerSuppliesColor = "1.3.6.1.2.1.43.12.1.1.4";
    private static readonly string OidPrtMarkerSuppliesMax = "1.3.6.1.2.1.43.12.1.1.5";
    private static readonly string OidPrtMarkerSuppliesLevel = "1.3.6.1.2.1.43.12.1.1.8";

    // Printer counter (page counts)
    private static readonly string OidPrtMarkerLifeCount = "1.3.6.1.2.1.43.10.2.1.4";

    // Printer input (paper trays)
    private static readonly string OidPrtInputType = "1.3.6.1.2.1.43.8.2.1.10";
    private static readonly string OidPrtInputLevel = "1.3.6.1.2.1.43.8.2.1.9";
    private static readonly string OidPrtInputMax = "1.3.6.1.2.1.43.8.2.1.8";

    // ── Public API ─────────────────────────────────────────────────────────

    public async Task<SnmpProbeResult?> ProbeAsync(
        string ipAddress,
        SnmpConnectionInfo connection,
        int timeoutMs = 2000,
        CancellationToken ct = default)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), 161);

        try
        {
            // Fire all 3 GETs concurrently — faster than sequential
            var descrTask = GetAsync(endpoint, connection, OidSysDescr, timeoutMs, ct);
            var objectIdTask = GetAsync(endpoint, connection, OidSysObjectID, timeoutMs, ct);
            var nameTask = GetAsync(endpoint, connection, OidSysName, timeoutMs, ct);

            await Task.WhenAll(descrTask, objectIdTask, nameTask);

            var sysDescr = await descrTask;
            var sysObjectId = await objectIdTask;
            var sysName = await nameTask;

            // If all 3 came back null the host is not speaking SNMP
            if (sysDescr is null && sysObjectId is null && sysName is null)
                return null;

            return new SnmpProbeResult
            {
                SysDescr = sysDescr,
                SysObjectId = sysObjectId,
                SysName = sysName,
                DeviceType = FingerprintDeviceType(sysDescr, sysObjectId),
                IsSnmpReachable = true
            };
        }
        catch (InvalidOperationException)
        {
            throw; // re-throw config errors
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "SNMP probe failed for {IpAddress}", ipAddress);
            return null; // host not reachable on SNMP — not an error
        }
    }

    public async Task<SnmpDiscoveryResult?> DiscoverAsync(
        string ipAddress,
        SnmpConnectionInfo connection,
        int timeoutMs = 3000,
        CancellationToken ct = default)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), 161);
        var result = new SnmpDiscoveryResult();

        try
        {
            // ── System info (concurrent GETs) ─────────────────────────────
            var descrTask = GetAsync(endpoint, connection, OidSysDescr, timeoutMs, ct);
            var objectIdTask = GetAsync(endpoint, connection, OidSysObjectID, timeoutMs, ct);
            var nameTask = GetAsync(endpoint, connection, OidSysName, timeoutMs, ct);
            var locationTask = GetAsync(endpoint, connection, OidSysLocation, timeoutMs, ct);
            var contactTask = GetAsync(endpoint, connection, OidSysContact, timeoutMs, ct);
            var uptimeTask = GetAsync(endpoint, connection, OidSysUpTime, timeoutMs, ct);

            await Task.WhenAll(descrTask, objectIdTask, nameTask, locationTask, contactTask, uptimeTask);

            result.SysDescr = await descrTask;
            result.SysObjectId = await objectIdTask;
            result.SysName = await nameTask;
            result.SysLocation = await locationTask;
            result.SysContact = await contactTask;

            if (long.TryParse(await uptimeTask, out var uptime))
                result.SysUpTime = uptime;

            // If nothing responded, host is not SNMP-capable
            if (result.SysDescr is null && result.SysObjectId is null)
                return null;

            // ── Fingerprint device ────────────────────────────────────────
            result.DeviceType = FingerprintDeviceType(result.SysDescr, result.SysObjectId);
            (result.Vendor, result.Model, result.OsVersion) = ParseSysDescr(result.SysDescr);

            // ── Interface table (walk) ────────────────────────────────────
            result.Interfaces = await DiscoverInterfacesAsync(endpoint, connection, timeoutMs, ct);

            // ── Storage (walk) ────────────────────────────────────────────
            result.Storage = await DiscoverStorageAsync(endpoint, connection, timeoutMs, ct);

            // ── Installed software (walk) ─────────────────────────────────
            result.InstalledSoftware = await DiscoverSoftwareAsync(endpoint, connection, timeoutMs, ct);

            return result;
        }
        catch (InvalidOperationException)
        {
            throw; // re-throw config errors (null community string, etc.)
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SNMP discovery failed for {IpAddress}", ipAddress);
            return null;
        }
    }

    public async Task<PrinterMibData?> GetPrinterDataAsync(
        string ipAddress,
        SnmpConnectionInfo connection,
        int timeoutMs = 2000,
        CancellationToken ct = default)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), 161);
        var data = new PrinterMibData();

        try
        {
            // ── General printer info ──────────────────────────────────────
            var statusTask = GetAsync(endpoint, connection, OidPrtGeneralStatus, timeoutMs, ct);
            var errorStateTask = GetAsync(endpoint, connection, OidPrtGeneralErrorState, timeoutMs, ct);
            var serialTask = GetAsync(endpoint, connection, OidPrtGeneralSerialNum, timeoutMs, ct);
            var firmwareTask = GetAsync(endpoint, connection, OidPrtGeneralFirmware, timeoutMs, ct);

            await Task.WhenAll(statusTask, errorStateTask, serialTask, firmwareTask);

            if (int.TryParse(await statusTask, out var status))
                data.PrinterStatus = status;
            if (int.TryParse(await errorStateTask, out var errorState))
                data.PrinterErrorState = errorState;
            data.SerialNumber = await serialTask;
            data.FirmwareVersion = await firmwareTask;

            // ── Page counts ───────────────────────────────────────────────
            var lifeCount = await GetAsync(endpoint, connection, OidPrtMarkerLifeCount, timeoutMs, ct);
            if (long.TryParse(lifeCount, out var total))
                data.TotalPagesPrinted = total;

            // ── Toner levels (walk) ───────────────────────────────────────
            data.TonerLevels = await WalkTonerLevelsAsync(endpoint, connection, timeoutMs, ct);

            // ── Paper trays (walk) ────────────────────────────────────────
            data.PaperTrays = await WalkPaperTraysAsync(endpoint, connection, timeoutMs, ct);

            return data;
        }
        catch (InvalidOperationException)
        {
            throw; // re-throw config errors
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Printer MIB query failed for {IpAddress}", ipAddress);
            return null;
        }
    }

    public async Task<PortMappingResult?> GetPortMappingAsync(
        string ipAddress,
        SnmpConnectionInfo connection,
        int timeoutMs = 3000,
        CancellationToken ct = default)
    {
        var endpoint = new IPEndPoint(IPAddress.Parse(ipAddress), 161);
        var result = new PortMappingResult();

        try
        {
            // Walk ifTable for port info
            var ifDescrWalk = await WalkAsync(endpoint, connection, OidIfDescr, timeoutMs, ct);
            var ifTypeWalk = await WalkAsync(endpoint, connection, OidIfType, timeoutMs, ct);
            var ifSpeedWalk = await WalkAsync(endpoint, connection, OidIfSpeed, timeoutMs, ct);
            var ifPhysAddrWalk = await WalkAsync(endpoint, connection, OidIfPhysAddress, timeoutMs, ct);
            var ifAdminWalk = await WalkAsync(endpoint, connection, OidIfAdminStatus, timeoutMs, ct);
            var ifOperWalk = await WalkAsync(endpoint, connection, OidIfOperStatus, timeoutMs, ct);

            // Merge by ifIndex (the last segment of the OID)
            var maxCount = new[] { ifDescrWalk.Count, ifTypeWalk.Count, ifSpeedWalk.Count,
                                   ifPhysAddrWalk.Count, ifAdminWalk.Count, ifOperWalk.Count }.Max();

            for (int i = 0; i < maxCount; i++)
            {
                var port = new PortInfo
                {
                    IfIndex = i + 1,
                    IfDescription = ifDescrWalk.Count > i ? ifDescrWalk[i].Data.ToString() : null,
                    IfType = ifTypeWalk.Count > i ? GetIntValue(ifTypeWalk[i].Data) : null,
                    Speed = ifSpeedWalk.Count > i ? GetLongValue(ifSpeedWalk[i].Data) : null,
                    MacAddress = ifPhysAddrWalk.Count > i ? FormatMacAddress(ifPhysAddrWalk[i].Data) : null,
                    AdminStatus = ifAdminWalk.Count > i ? GetIntValue(ifAdminWalk[i].Data) : null,
                    OperStatus = ifOperWalk.Count > i ? GetIntValue(ifOperWalk[i].Data) : null
                };

                // Derive IfName from IfDescription (common pattern: "GigabitEthernet0/1" → "Gi0/1")
                port.IfName = AbbreviateIfName(port.IfDescription);

                result.Ports.Add(port);
            }

            result.TotalPorts = result.Ports.Count;
            result.ActivePorts = result.Ports.Count(p => p.AdminStatus == 1 && p.OperStatus == 1);
            result.DownPorts = result.Ports.Count(p => p.AdminStatus == 1 && p.OperStatus == 2);

            return result;
        }
        catch (InvalidOperationException)
        {
            throw; // re-throw config errors
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Port mapping query failed for {IpAddress}", ipAddress);
            return null;
        }
    }

    public string? FingerprintDeviceType(string? sysDescr, string? sysObjectId)
    {
        if (string.IsNullOrWhiteSpace(sysDescr) && string.IsNullOrWhiteSpace(sysObjectId))
            return null;

        var desc = (sysDescr ?? string.Empty).ToLowerInvariant();

        // ── Check sysDescr keywords ────────────────────────────────────────
        if (desc.Contains("printer") || desc.Contains("jetdirect") ||
            desc.Contains("laserjet") || desc.Contains("officejet") ||
            desc.Contains("imagerunner") || desc.Contains("workcentre"))
            return "Printer";

        if (desc.Contains("cisco") || desc.Contains("catalyst") ||
            desc.Contains("juniper") || desc.Contains("extreme") ||
            desc.Contains("procurve") || desc.Contains("aruba") ||
            desc.Contains("fortinet") || desc.Contains("fortigate"))
            return "NetworkDevice";

        if (desc.Contains("esxi") || desc.Contains("vmware"))
            return "VirtualMachineHost";

        if (desc.Contains("windows") || desc.Contains("microsoft"))
            return "Computer";

        if (desc.Contains("linux") || desc.Contains("ubuntu") ||
            desc.Contains("debian") || desc.Contains("centos") ||
            desc.Contains("red hat") || desc.Contains("freebsd") ||
            desc.Contains("alpine"))
            return "Computer";

        if (desc.Contains("ups") || desc.Contains("powerware") ||
            desc.Contains("apc ") || desc.Contains("eaton") ||
            desc.Contains("liebert"))
            return "PowerDevice";

        if (desc.Contains("storage") || desc.Contains("nas") ||
            desc.Contains("qnap") || desc.Contains("synology") ||
            desc.Contains("netapp") || desc.Contains("emc"))
            return "Storage";

        if (desc.Contains("camera") || desc.Contains("axis") ||
            desc.Contains("hikvision") || desc.Contains("dahua"))
            return "Camera";

        if (desc.Contains("voip") || desc.Contains("phone") ||
            desc.Contains("asterisk") || desc.Contains("polycom") ||
            desc.Contains("cisco ip phone"))
            return "Phone";

        // ── Check sysObjectID enterprise prefixes ──────────────────────────
        if (!string.IsNullOrWhiteSpace(sysObjectId))
        {
            if (sysObjectId.StartsWith("1.3.6.1.4.1.9."))
                return "NetworkDevice";     // Cisco
            if (sysObjectId.StartsWith("1.3.6.1.4.1.11."))
                return "Printer";         // HP
            if (sysObjectId.StartsWith("1.3.6.1.4.1.2636."))
                return "NetworkDevice"; // Juniper
            if (sysObjectId.StartsWith("1.3.6.1.4.1.318."))
                return "PowerDevice";    // APC
            if (sysObjectId.StartsWith("1.3.6.1.4.1.232."))
                return "Computer";       // HPE
            if (sysObjectId.StartsWith("1.3.6.1.4.1.674."))
                return "Computer";       // Dell
            if (sysObjectId.StartsWith("1.3.6.1.4.1.6027."))
                return "NetworkDevice"; // Force10
            if (sysObjectId.StartsWith("1.3.6.1.4.1.1916."))
                return "NetworkDevice"; // Extreme
            if (sysObjectId.StartsWith("1.3.6.1.4.1.25461."))
                return "NetworkDevice";// Fortinet
            if (sysObjectId.StartsWith("1.3.6.1.4.1.14823."))
                return "NetworkDevice";// Aruba
            if (sysObjectId.StartsWith("1.3.6.1.4.1.6876."))
                return "Computer";      // QNAP
            if (sysObjectId.StartsWith("1.3.6.1.4.1.6574."))
                return "Storage";       // Synology
            if (sysObjectId.StartsWith("1.3.6.1.4.1.388."))
                return "Camera";         // Axis
        }

        return "Unknown";
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<List<SnmpInterfaceInfo>> DiscoverInterfacesAsync(
        IPEndPoint endpoint,
        SnmpConnectionInfo connection,
        int timeoutMs,
        CancellationToken ct)
    {
        var interfaces = new List<SnmpInterfaceInfo>();

        try
        {
            var descrs = await WalkAsync(endpoint, connection, OidIfDescr, timeoutMs, ct);
            var types = await WalkAsync(endpoint, connection, OidIfType, timeoutMs, ct);
            var speeds = await WalkAsync(endpoint, connection, OidIfSpeed, timeoutMs, ct);
            var macs = await WalkAsync(endpoint, connection, OidIfPhysAddress, timeoutMs, ct);
            var adminStatuses = await WalkAsync(endpoint, connection, OidIfAdminStatus, timeoutMs, ct);
            var operStatuses = await WalkAsync(endpoint, connection, OidIfOperStatus, timeoutMs, ct);

            for (int i = 0; i < descrs.Count; i++)
            {
                var iface = new SnmpInterfaceInfo
                {
                    Index = i + 1,
                    Description = descrs[i].Data.ToString(),
                    Type = types.Count > i ? GetIntValue(types[i].Data) : null,
                    Speed = speeds.Count > i ? GetLongValue(speeds[i].Data) : null,
                    MacAddress = macs.Count > i ? FormatMacAddress(macs[i].Data) : null,
                    AdminStatus = adminStatuses.Count > i ? GetIntValue(adminStatuses[i].Data) : null,
                    OperStatus = operStatuses.Count > i ? GetIntValue(operStatuses[i].Data) : null
                };

                if (!string.IsNullOrWhiteSpace(iface.Description))
                    interfaces.Add(iface);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Interface discovery failed for {Endpoint}", endpoint);
        }

        return interfaces;
    }

    private async Task<List<StorageInfo>> DiscoverStorageAsync(
        IPEndPoint endpoint,
        SnmpConnectionInfo connection,
        int timeoutMs,
        CancellationToken ct)
    {
        var storage = new List<StorageInfo>();

        try
        {
            var descrs = await WalkAsync(endpoint, connection, OidHrStorageDescr, timeoutMs, ct);
            var sizes = await WalkAsync(endpoint, connection, OidHrStorageSize, timeoutMs, ct);
            var used = await WalkAsync(endpoint, connection, OidHrStorageUsed, timeoutMs, ct);
            var types = await WalkAsync(endpoint, connection, OidHrStorageType, timeoutMs, ct);

            for (int i = 0; i < descrs.Count; i++)
            {
                var descr = descrs[i].Data.ToString();
                var size = sizes.Count > i ? GetLongValue(sizes[i].Data) : null;

                if (size > 0)
                {
                    storage.Add(new StorageInfo
                    {
                        Description = descr,
                        Size = size,
                        Used = used.Count > i ? GetLongValue(used[i].Data) : null,
                        Type = types.Count > i ? types[i].Data.ToString() : null
                    });
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Storage discovery failed for {Endpoint}", endpoint);
        }

        return storage;
    }

    private async Task<List<SoftwareInfo>> DiscoverSoftwareAsync(
        IPEndPoint endpoint,
        SnmpConnectionInfo connection,
        int timeoutMs,
        CancellationToken ct)
    {
        var software = new List<SoftwareInfo>();

        try
        {
            var names = await WalkAsync(endpoint, connection, OidHrSWInstalledName, timeoutMs, ct);
            var versions = await WalkAsync(endpoint, connection, OidHrSWInstalledVer, timeoutMs, ct);
            var vendors = await WalkAsync(endpoint, connection, OidHrSWInstalledVendor, timeoutMs, ct);
            var types = await WalkAsync(endpoint, connection, OidHrSWInstalledType, timeoutMs, ct);

            for (int i = 0; i < names.Count; i++)
            {
                software.Add(new SoftwareInfo
                {
                    Name = names[i].Data.ToString(),
                    Version = versions.Count > i ? versions[i].Data.ToString() : null,
                    Vendor = vendors.Count > i ? vendors[i].Data.ToString() : null,
                    Type = types.Count > i ? GetIntValue(types[i].Data) : null
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Software discovery failed for {Endpoint}", endpoint);
        }

        return software;
    }

    private async Task<List<TonerLevel>> WalkTonerLevelsAsync(
        IPEndPoint endpoint,
        SnmpConnectionInfo connection,
        int timeoutMs,
        CancellationToken ct)
    {
        var levels = new List<TonerLevel>();

        try
        {
            var colors = await WalkAsync(endpoint, connection, OidPrtMarkerSuppliesColor, timeoutMs, ct);
            var maxCaps = await WalkAsync(endpoint, connection, OidPrtMarkerSuppliesMax, timeoutMs, ct);
            var currentLevels = await WalkAsync(endpoint, connection, OidPrtMarkerSuppliesLevel, timeoutMs, ct);

            for (int i = 0; i < currentLevels.Count; i++)
            {
                var level = GetIntValue(currentLevels[i].Data) ?? -1;
                // Skip -1 (unknown/not applicable) entries
                if (level == -1)
                    continue;

                levels.Add(new TonerLevel
                {
                    Color = colors.Count > i ? MapTonerColor(colors[i].Data.ToString()) : "Unknown",
                    Level = level,
                    MaxCapacity = maxCaps.Count > i ? maxCaps[i].Data.ToString() : null,
                    CurrentLevel = currentLevels[i].Data.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Toner level walk failed for {Endpoint}", endpoint);
        }

        return levels;
    }

    private async Task<List<PaperTrayInfo>> WalkPaperTraysAsync(
        IPEndPoint endpoint,
        SnmpConnectionInfo connection,
        int timeoutMs,
        CancellationToken ct)
    {
        var trays = new List<PaperTrayInfo>();

        try
        {
            var types = await WalkAsync(endpoint, connection, OidPrtInputType, timeoutMs, ct);
            var levels = await WalkAsync(endpoint, connection, OidPrtInputLevel, timeoutMs, ct);
            var maxCaps = await WalkAsync(endpoint, connection, OidPrtInputMax, timeoutMs, ct);

            for (int i = 0; i < types.Count; i++)
            {
                trays.Add(new PaperTrayInfo
                {
                    Index = i + 1,
                    Type = MapPaperTrayType(types[i].Data.ToString()),
                    CurrentLevel = levels.Count > i ? GetIntValue(levels[i].Data) : null,
                    MaxCapacity = maxCaps.Count > i ? GetIntValue(maxCaps[i].Data) : null
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Paper tray walk failed for {Endpoint}", endpoint);
        }

        return trays;
    }

    // ── SNMP GET helper ────────────────────────────────────────────────────
    // NOTE: SNMPv1/v2c use community strings. SNMPv3 requires USM security
    // parameters (engine ID discovery, auth/priv protocols). For v3, the
    // Messenger.Get overload with IPEndPoint, OctetString (engine ID),
    // SecurityParameters, and IAuthenticationScheme is needed.
    // Current implementation supports v1/v2c only.

    private static async Task<string?> GetAsync(
        IPEndPoint endpoint,
        SnmpConnectionInfo connection,
        string oid,
        int timeoutMs,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            try
            {
                var variables = new List<Variable> { new(new ObjectIdentifier(oid)) };
                var version = MapVersion(connection.Version);
                var community = new OctetString(connection.CommunityString
                    ?? throw new InvalidOperationException("CommunityString is required for SNMPv1/v2c"));

                var response = Messenger.Get(
                    version,
                    endpoint,
                    community,
                    variables,
                    timeoutMs);

                return response.FirstOrDefault()?.Data.ToString();
            }
            catch (InvalidOperationException)
            {
                throw; // re-throw config errors
            }
            catch
            {
                return null;
            }
        }, ct);
    }

    // ── SNMP WALK helper ───────────────────────────────────────────────────

    private static async Task<List<Variable>> WalkAsync(
        IPEndPoint endpoint,
        SnmpConnectionInfo connection,
        string rootOid,
        int timeoutMs,
        CancellationToken ct)
    {
        var results = new List<Variable>();

        await Task.Run(() =>
        {
            try
            {
                var community = new OctetString(connection.CommunityString
                    ?? throw new InvalidOperationException("CommunityString is required for SNMPv1/v2c"));
                Messenger.Walk(
                    MapVersion(connection.Version),
                    endpoint,
                    community,
                    new ObjectIdentifier(rootOid),
                    results,
                    timeoutMs,
                    WalkMode.WithinSubtree);
            }
            catch (InvalidOperationException)
            {
                throw; // re-throw config errors
            }
            catch
            {
                // Walk failures are non-fatal — device may not support all OIDs
            }
        }, ct);

        return results;
    }

    // ── Protocol mapping helpers ───────────────────────────────────────────

    private static VersionCode MapVersion(SnmpVersion version)
    {
        return version switch
        {
            SnmpVersion.V1 => VersionCode.V1,
            SnmpVersion.V2c => VersionCode.V2,
            SnmpVersion.V3 => VersionCode.V3,
            _ => VersionCode.V2
        };
    }

    // ── sysDescr parsing ───────────────────────────────────────────────────

    private static (string? Vendor, string? Model, string? OsVersion) ParseSysDescr(string? sysDescr)
    {
        if (string.IsNullOrWhiteSpace(sysDescr))
            return (null, null, null);

        var parts = sysDescr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return (null, null, sysDescr);

        // Common patterns:
        // "Cisco IOS Software, C3750 Software"
        // "HP LaserJet Pro MFP M428"
        // "Linux hostname 5.4.0-42-generic"
        // "Windows Server 2019"

        var vendor = parts[0];
        var model = parts.Length > 1 ? parts[1] : null;
        var version = sysDescr;

        return (vendor, model, version);
    }

    // ── Data conversion helpers ────────────────────────────────────────────

    private static int? GetIntValue(ISnmpData data)
    {
        var str = data.ToString();
        return int.TryParse(str, out var val) ? val : null;
    }

    private static long? GetLongValue(ISnmpData data)
    {
        var str = data.ToString();
        return long.TryParse(str, out var val) ? val : null;
    }

    private static string? FormatMacAddress(ISnmpData data)
    {
        if (data is OctetString octet)
        {
            var raw = octet.GetRaw();
            if (raw.Length == 6)
                return BitConverter.ToString(raw).Replace("-", ":").ToUpperInvariant();
        }
        return null;
    }

    private static string? AbbreviateIfName(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return null;

        // Common abbreviations for switch/router interfaces
        var abbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["GigabitEthernet"] = "Gi",
            ["FastEthernet"] = "Fa",
            ["TenGigabitEthernet"] = "Te",
            ["TwentyFiveGigE"] = "Te",
            ["HundredGigE"] = "Hu",
            ["Ethernet"] = "Et",
            ["Loopback"] = "Lo",
            ["Vlan"] = "Vl",
            ["Port-channel"] = "Po",
            ["Management"] = "Mg"
        };

        foreach (var (full, abbr) in abbreviations)
        {
            if (description.StartsWith(full, StringComparison.OrdinalIgnoreCase))
                return abbr + description[full.Length..];
        }

        return description;
    }

    private static string MapTonerColor(string? colorCode)
    {
        // RFC 3805 prtMarkerSuppliesColorantIndex values
        return colorCode?.ToLowerInvariant() switch
        {
            "1" or "black" => "Black",
            "2" or "cyan" => "Cyan",
            "3" or "magenta" => "Magenta",
            "4" or "yellow" => "Yellow",
            "5" or "white" => "White",
            "6" or "red" => "Red",
            "7" or "green" => "Green",
            "8" or "blue" => "Blue",
            _ => colorCode ?? "Unknown"
        };
    }

    private static string MapPaperTrayType(string? typeCode)
    {
        // RFC 3805 prtInputType values
        return typeCode?.ToLowerInvariant() switch
        {
            "1" or "other" => "Other",
            "2" or "unknown" => "Unknown",
            "3" or "removabletray" => "Removable Tray",
            "4" or "fixedtray" => "Fixed Tray",
            "5" or "cassetteribbon" => "Cassette Ribbon",
            "6" or "multiroll" => "Multi-Roll",
            _ => typeCode ?? "Unknown"
        };
    }
}
