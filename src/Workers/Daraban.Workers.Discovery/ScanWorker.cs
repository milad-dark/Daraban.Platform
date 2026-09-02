using System.Net;
using Daraban.Modules.Discovery.Data.Entities;
using Daraban.Modules.Discovery.Services;

namespace Daraban.Workers.Discovery;

/// <summary>
/// Background service that processes queued discovery scans.
/// Picks up scans from the database, expands CIDR ranges to IP lists,
/// performs ICMP/TCP/SNMP discovery on each IP, and stores results.
/// </summary>
public class ScanWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScanWorker> _logger;
    private readonly IPScannerOptions _options;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(10);

    public ScanWorker(
        IServiceProvider serviceProvider,
        ILogger<ScanWorker> logger,
        IPScannerOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScanWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedScansAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scans");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("ScanWorker stopped");
    }

    private async Task ProcessQueuedScansAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var discoveryService = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();
        var scanner = scope.ServiceProvider.GetRequiredService<IIPScanner>();

        // Get the next queued scan
        var scan = await discoveryService.GetQueuedScanAsync(ct);
        if (scan == null) return;

        _logger.LogInformation("Processing scan {ScanId} for range {RangeId}", scan.Id, scan.RangeId);

        try
        {
            // Mark as running
            await discoveryService.UpdateScanStatusAsync(scan.Id, ScanStatus.Running, ct: ct);

            // Get range details
            var range = await discoveryService.GetRangeByIdAsync(scan.RangeId, ct);
            if (range == null)
            {
                await discoveryService.UpdateScanStatusAsync(
                    scan.Id, ScanStatus.Failed, "Range not found", ct);
                return;
            }

            // Expand CIDR to IP list
            var ipList = ExpandCidr(range.CidrRange, range.StartIp, range.EndIp);
            var totalIps = ipList.Count;

            _logger.LogInformation(
                "Scan {ScanId}: expanded {Cidr} to {Count} IPs",
                scan.Id, range.CidrRange, totalIps);

            // Update total IP count
            await discoveryService.UpdateScanCountsAsync(scan.Id, 0, 0, totalIps, ct);

            // ICMP ping sweep
            _logger.LogInformation("Scan {ScanId}: starting ICMP ping sweep", scan.Id);
            var pingResults = await scanner.PingSweepAsync(
                ipList, _options.PingTimeoutMs, ct);

            var aliveIps = pingResults
                .Where(p => p.IsAlive)
                .Select(p => p.IpAddress)
                .ToList();

            _logger.LogInformation(
                "Scan {ScanId}: {Alive}/{Total} IPs responded to ping",
                scan.Id, aliveIps.Count, totalIps);

            // Process each alive host
            var devices = new List<DeviceResponse>();
            var processedCount = 0;

            foreach (var ip in aliveIps)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var device = await DiscoverDeviceAsync(scanner, ip, scan.Id, range, ct);
                    if (device != null)
                    {
                        devices.Add(device);
                        processedCount++;
                    }

                    // Rate limiting - delay between devices
                    if (_options.DeviceDelayMs > 0)
                    {
                        await Task.Delay(_options.DeviceDelayMs, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to discover device at {IpAddress}", ip);
                }
            }

            // Store devices
            if (devices.Any())
            {
                await discoveryService.AddDevicesAsync(scan.Id, scan.RangeId, devices, ct);
            }

            // Update scan counts
            await discoveryService.UpdateScanCountsAsync(
                scan.Id, devices.Count, aliveIps.Count, totalIps, ct);

            // Mark as completed
            await discoveryService.UpdateScanStatusAsync(scan.Id, ScanStatus.Completed, ct: ct);

            _logger.LogInformation(
                "Scan {ScanId} completed: {Devices} devices found",
                scan.Id, devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan {ScanId} failed", scan.Id);
            await discoveryService.UpdateScanStatusAsync(
                scan.Id, ScanStatus.Failed, ex.Message, ct);
        }
    }

    private async Task<DeviceResponse?> DiscoverDeviceAsync(
        IIPScanner scanner,
        string ipAddress,
        Guid scanId,
        RangeResponse range,
        CancellationToken ct)
    {
        // TCP port scan
        var portResults = await scanner.TcpPortScanAsync(
            ipAddress, _options.DefaultPorts, _options.PortScanTimeoutMs, ct);

        var openPorts = portResults
            .Where(p => p.IsOpen)
            .Select(p => p.Port)
            .ToList();

        // Reverse DNS
        var hostname = await scanner.ReverseDnsLookupAsync(ipAddress, ct);

        // OS fingerprinting
        var osFingerprint = await scanner.OsFingerprintAsync(
            ipAddress, _options.DefaultPorts.Take(5), ct);

        return new DeviceResponse(
            Id: 0, // Will be assigned by DB
            ScanId: scanId,
            RangeId: range.Id,
            IpAddress: ipAddress,
            MacAddress: null, // Will be obtained via ARP or SNMP later
            Hostname: hostname ?? osFingerprint?.Hostname,
            OsGuess: osFingerprint?.OsGuess,
            OsVersion: osFingerprint?.OsVersion,
            Vendor: null,
            Model: null,
            SerialNumber: null,
            OpenPorts: openPorts.Any() ? string.Join(",", openPorts) : null,
            SysDescr: null,
            SysName: hostname,
            SysLocation: null,
            SysContact: null,
            SnmpUptime: null,
            PingMs: null,
            Ttl: osFingerprint?.Ttl,
            AssetCreated: false,
            AssetId: null,
            DiscoveredAt: DateTimeOffset.UtcNow,
            LastSeenAt: DateTimeOffset.UtcNow
        );
    }

    private List<string> ExpandCidr(string? cidrRange, string? startIp, string? endIp)
    {
        var ips = new List<string>();

        // If CIDR is provided, expand it
        if (!string.IsNullOrEmpty(cidrRange))
        {
            try
            {
                var parts = cidrRange.Split('/');
                if (parts.Length == 2 && int.TryParse(parts[1], out var prefixLength))
                {
                    var baseAddress = IPAddress.Parse(parts[0]);
                    ips = ExpandCidrToIps(baseAddress, prefixLength);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse CIDR {Cidr}", cidrRange);
            }
        }

        // If CIDR expansion failed or not provided, use start/end IPs
        if (!ips.Any() && !string.IsNullOrEmpty(startIp) && !string.IsNullOrEmpty(endIp))
        {
            ips = ExpandIpRange(startIp, endIp);
        }

        return ips;
    }

    private List<string> ExpandCidrToIps(IPAddress baseAddress, int prefixLength)
    {
        var ips = new List<string>();
        var bytes = baseAddress.GetAddressBytes();
        
        // Calculate number of hosts
        var hostBits = 32 - prefixLength;
        var numHosts = (1L << hostBits) - 2; // Exclude network and broadcast
        
        if (numHosts <= 0 || numHosts > 65536) // Cap at /16
        {
            _logger.LogWarning("CIDR expansion too large: {PrefixLength} bits", prefixLength);
            return ips;
        }

        // Convert base address to uint
        var baseUint = BitConverter.ToUInt32(bytes.Reverse().ToArray(), 0);
        
        // Skip network address, iterate through hosts
        for (long i = 1; i <= numHosts; i++)
        {
            var hostAddress = baseUint + (uint)i;
            var hostBytes = BitConverter.GetBytes(hostAddress).Reverse().ToArray();
            ips.Add(new IPAddress(hostBytes).ToString());
        }

        return ips;
    }

    private List<string> ExpandIpRange(string startIp, string endIp)
    {
        var ips = new List<string>();
        
        try
        {
            var start = IPAddress.Parse(startIp).GetAddressBytes();
            var end = IPAddress.Parse(endIp).GetAddressBytes();
            
            var startUint = BitConverter.ToUInt32(start.Reverse().ToArray(), 0);
            var endUint = BitConverter.ToUInt32(end.Reverse().ToArray(), 0);
            
            // Cap at 65536 IPs
            if (endUint - startUint > 65536)
            {
                _logger.LogWarning("IP range too large, capping at 65536");
                endUint = startUint + 65535;
            }
            
            for (var i = startUint; i <= endUint; i++)
            {
                var bytes = BitConverter.GetBytes(i).Reverse().ToArray();
                ips.Add(new IPAddress(bytes).ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to expand IP range {Start}-{End}", startIp, endIp);
        }

        return ips;
    }
}
