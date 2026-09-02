using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Daraban.Workers.Discovery;

/// <summary>
/// IP scanning engine implementing ICMP ping, TCP port scan, DNS lookup,
/// NetBIOS resolution, and OS fingerprinting (Task 5.3).
/// </summary>
public class IPScannerEngine : IIPScanner
{
    private readonly ILogger<IPScannerEngine> _logger;
    private readonly IPScannerOptions _options;

    public IPScannerEngine(ILogger<IPScannerEngine> logger, IPScannerOptions options)
    {
        _logger = logger;
        _options = options;
    }

    /// <summary>
    /// ICMP ping sweep across multiple IP addresses.
    /// </summary>
    public async Task<List<PingResult>> PingSweepAsync(
        IEnumerable<string> ipAddresses,
        int timeoutMs = 2000,
        CancellationToken ct = default)
    {
        var results = new List<PingResult>();
        var semaphore = new SemaphoreSlim(_options.MaxConcurrentPings);
        
        var tasks = ipAddresses.Select(async ip =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var result = await PingSingleAsync(ip, timeoutMs, ct);
                lock (results)
                {
                    results.Add(result);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    private async Task<PingResult> PingSingleAsync(string ipAddress, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ipAddress, timeoutMs);
            
            return new PingResult(
                IpAddress: ipAddress,
                IsAlive: reply.Status == IPStatus.Success,
                RoundTripMs: reply.RoundtripTime,
                Ttl: reply.Options?.Ttl
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ping failed for {IpAddress}", ipAddress);
            return new PingResult(
                IpAddress: ipAddress,
                IsAlive: false,
                RoundTripMs: null,
                Ttl: null,
                ErrorMessage: ex.Message
            );
        }
    }

    /// <summary>
    /// TCP port scan on a single host.
    /// </summary>
    public async Task<List<PortScanResult>> TcpPortScanAsync(
        string ipAddress,
        IEnumerable<int> ports,
        int timeoutMs = 1000,
        CancellationToken ct = default)
    {
        var results = new List<PortScanResult>();
        var semaphore = new SemaphoreSlim(_options.MaxConcurrentPortScans);
        
        var tasks = ports.Select(async port =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var result = await ScanSinglePortAsync(ipAddress, port, timeoutMs, ct);
                lock (results)
                {
                    results.Add(result);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results;
    }

    private async Task<PortScanResult> ScanSinglePortAsync(
        string ipAddress,
        int port,
        int timeoutMs,
        CancellationToken ct)
    {
        var client = new TcpClient();
        try
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            await client.ConnectAsync(ipAddress, port, linkedCts.Token);
            
            // Grab banner if connected
            string? banner = null;
            if (client.Connected)
            {
                try
                {
                    client.GetStream().ReadTimeout = 1000;
                    var buffer = new byte[1024];
                    var bytesRead = await client.GetStream().ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead > 0)
                    {
                        banner = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                    }
                }
                catch
                {
                    // Banner grab failed, continue
                }
            }

            return new PortScanResult(
                IpAddress: ipAddress,
                Port: port,
                IsOpen: true,
                ServiceName: GetServiceName(port),
                Banner: banner
            );
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return new PortScanResult(
                IpAddress: ipAddress,
                Port: port,
                IsOpen: false,
                ServiceName: GetServiceName(port),
                ErrorMessage: "Scan cancelled"
            );
        }
        catch (OperationCanceledException)
        {
            return new PortScanResult(
                IpAddress: ipAddress,
                Port: port,
                IsOpen: false,
                ServiceName: GetServiceName(port),
                ErrorMessage: "Connection timeout"
            );
        }
        catch (SocketException)
        {
            return new PortScanResult(
                IpAddress: ipAddress,
                Port: port,
                IsOpen: false,
                ServiceName: GetServiceName(port)
            );
        }
        catch (Exception ex)
        {
            return new PortScanResult(
                IpAddress: ipAddress,
                Port: port,
                IsOpen: false,
                ServiceName: GetServiceName(port),
                ErrorMessage: ex.Message
            );
        }
        finally
        {
            client.Dispose();
        }
    }

    /// <summary>
    /// Reverse DNS lookup.
    /// </summary>
    public async Task<string?> ReverseDnsLookupAsync(string ipAddress, CancellationToken ct = default)
    {
        try
        {
            var entry = await Dns.GetHostEntryAsync(ipAddress);
            return entry.HostName;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Reverse DNS lookup failed for {IpAddress}", ipAddress);
            return null;
        }
    }

    /// <summary>
    /// NetBIOS name resolution for Windows devices.
    /// </summary>
    public async Task<NetBiosInfo?> NetBiosResolveAsync(string ipAddress, CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ipAddress, 137, ct); // NetBIOS Name Service
            
            // NetBIOS Name Query for workstation service
            var query = BuildNetBiosQuery(ipAddress);
            var stream = client.GetStream();
            
            await stream.WriteAsync(query, ct);
            
            var buffer = new byte[1024];
            var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
            
            if (bytesRead > 0)
            {
                return ParseNetBiosResponse(buffer, bytesRead);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "NetBIOS resolution failed for {IpAddress}", ipAddress);
        }
        
        return null;
    }

    /// <summary>
    /// OS fingerprinting via TTL analysis and banner grabbing.
    /// </summary>
    public async Task<OsFingerprint?> OsFingerprintAsync(
        string ipAddress,
        IEnumerable<int> commonPorts,
        CancellationToken ct = default)
    {
        try
        {
            // Get TTL from ping
            var pingResult = await PingSingleAsync(ipAddress, 2000, ct);
            
            // Get banners from common ports
            var banners = new Dictionary<int, string>();
            foreach (var port in commonPorts)
            {
                try
                {
                    using var client = new TcpClient();
                    var connectTask = client.ConnectAsync(ipAddress, port);
                    var timeoutTask = Task.Delay(1000, ct);
                    
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == connectTask && client.Connected)
                    {
                        client.GetStream().ReadTimeout = 1000;
                        var buffer = new byte[1024];
                        var bytesRead = await client.GetStream().ReadAsync(buffer, 0, buffer.Length, ct);
                        if (bytesRead > 0)
                        {
                            banners[port] = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();
                        }
                    }
                }
                catch
                {
                    // Port not open or banner grab failed
                }
            }

            // Get hostname via DNS
            string? hostname = null;
            try
            {
                var entry = await Dns.GetHostEntryAsync(ipAddress);
                hostname = entry.HostName;
            }
            catch { }

            // Fingerprint OS from TTL and banners
            var osGuess = FingerprintOs(pingResult.Ttl, banners);
            
            return new OsFingerprint(
                OsGuess: osGuess,
                OsVersion: ExtractOsVersion(osGuess, banners),
                Ttl: pingResult.Ttl,
                Banners: banners,
                Hostname: hostname
            );
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "OS fingerprint failed for {IpAddress}", ipAddress);
            return null;
        }
    }

    private string? FingerprintOs(int? ttl, Dictionary<int, string> banners)
    {
        if (ttl.HasValue)
        {
            // TTL-based OS guessing
            return ttl.Value switch
            {
                <= 64 => "Linux/Unix",
                <= 128 => "Windows",
                <= 255 => "Network Device (Cisco/Juniper)",
                _ => "Unknown"
            };
        }

        // Banner-based guessing
        foreach (var banner in banners.Values)
        {
            if (banner.Contains("Apache", StringComparison.OrdinalIgnoreCase) ||
                banner.Contains("nginx", StringComparison.OrdinalIgnoreCase))
                return "Linux/Unix (Web Server)";
            if (banner.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                banner.Contains("IIS", StringComparison.OrdinalIgnoreCase))
                return "Windows (IIS)";
            if (banner.Contains("OpenSSH", StringComparison.OrdinalIgnoreCase))
                return "Linux/Unix (SSH)";
        }

        return null;
    }

    private string? ExtractOsVersion(string? osGuess, Dictionary<int, string> banners)
    {
        if (osGuess == null) return null;

        // Try to extract version from banners
        foreach (var banner in banners.Values)
        {
            // Windows version patterns
            if (banner.Contains("Windows Server 2019", StringComparison.OrdinalIgnoreCase))
                return "Windows Server 2019";
            if (banner.Contains("Windows Server 2022", StringComparison.OrdinalIgnoreCase))
                return "Windows Server 2022";
            if (banner.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
                return "Windows 10";
            if (banner.Contains("Windows 11", StringComparison.OrdinalIgnoreCase))
                return "Windows 11";
            
            // Linux version patterns
            if (banner.Contains("Ubuntu", StringComparison.OrdinalIgnoreCase))
                return "Ubuntu Linux";
            if (banner.Contains("Debian", StringComparison.OrdinalIgnoreCase))
                return "Debian Linux";
            if (banner.Contains("CentOS", StringComparison.OrdinalIgnoreCase))
                return "CentOS Linux";
        }

        return null;
    }

    private byte[] BuildNetBiosQuery(string ipAddress)
    {
        // Simplified NetBIOS Name Query
        // In production, use a proper NetBIOS library
        return new byte[]
        {
            0x80, 0x94, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x20, 0x43, 0x4B, 0x41,
            0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
            0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
            0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41, 0x41,
            0x41, 0x00, 0x00, 0x21, 0x00, 0x01
        };
    }

    private NetBiosInfo? ParseNetBiosResponse(byte[] buffer, int length)
    {
        // Simplified parsing - in production, use a proper NetBIOS library
        // This is a placeholder for the actual NetBIOS response parsing
        try
        {
            if (length < 57) return null;
            
            // Extract names from the response
            var numNames = buffer[56];
            var offset = 57;
            
            string? computerName = null;
            string? domainName = null;
            
            for (int i = 0; i < numNames && offset + 18 <= length; i++)
            {
                var name = Encoding.ASCII.GetString(buffer, offset, 15).TrimEnd('\0');
                var flags = buffer[offset + 15];
                
                // Flags 0x04 = workstation, 0x00 = domain
                if (flags == 0x04 && computerName == null)
                    computerName = name;
                else if (flags == 0x00 && domainName == null)
                    domainName = name;
                    
                offset += 18;
            }
            
            return new NetBiosInfo(
                ComputerName: computerName,
                DomainName: domainName,
                UserName: null,
                MacAddress: null,
                MajorVersion: null,
                MinorVersion: null
            );
        }
        catch
        {
            return null;
        }
    }

    private string GetServiceName(int port) => port switch
    {
        21 => "FTP",
        22 => "SSH",
        23 => "Telnet",
        25 => "SMTP",
        53 => "DNS",
        80 => "HTTP",
        110 => "POP3",
        111 => "RPCBind",
        135 => "MSRPC",
        139 => "NetBIOS",
        143 => "IMAP",
        443 => "HTTPS",
        445 => "SMB",
        993 => "IMAPS",
        995 => "POP3S",
        1433 => "MSSQL",
        3306 => "MySQL",
        3389 => "RDP",
        5432 => "PostgreSQL",
        8080 => "HTTP-Alt",
        _ => "Unknown"
    };
}

/// <summary>
/// Options for IP scanner.
/// </summary>
public class IPScannerOptions
{
    public int MaxConcurrentPings { get; set; } = 50;
    public int MaxConcurrentPortScans { get; set; } = 100;
    public int PingTimeoutMs { get; set; } = 2000;
    public int PortScanTimeoutMs { get; set; } = 1000;
    public int DeviceDelayMs { get; set; } = 100;
    public List<int> DefaultPorts { get; set; } = new()
    {
        21, 22, 23, 25, 53, 80, 110, 135, 139, 143,
        443, 445, 993, 995, 1433, 3306, 3389, 5432, 8080
    };
}
