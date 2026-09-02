namespace Daraban.Workers.Discovery;

/// <summary>
/// IP scanning engine providing ICMP ping, TCP port scan, DNS lookup,
/// NetBIOS resolution, and OS fingerprinting capabilities.
/// </summary>
public interface IIPScanner
{
    /// <summary>
    /// Perform ICMP ping sweep across an IP range.
    /// </summary>
    Task<List<PingResult>> PingSweepAsync(IEnumerable<string> ipAddresses, int timeoutMs = 2000, CancellationToken ct = default);

    /// <summary>
    /// TCP port scan on a single host.
    /// </summary>
    Task<List<PortScanResult>> TcpPortScanAsync(string ipAddress, IEnumerable<int> ports, int timeoutMs = 1000, CancellationToken ct = default);

    /// <summary>
    /// Reverse DNS lookup for an IP address.
    /// </summary>
    Task<string?> ReverseDnsLookupAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// NetBIOS name resolution for Windows devices.
    /// </summary>
    Task<NetBiosInfo?> NetBiosResolveAsync(string ipAddress, CancellationToken ct = default);

    /// <summary>
    /// OS fingerprinting via TTL analysis and banner grabbing.
    /// </summary>
    Task<OsFingerprint?> OsFingerprintAsync(string ipAddress, IEnumerable<int> commonPorts, CancellationToken ct = default);
}

/// <summary>
/// Result of an ICMP ping.
/// </summary>
public record PingResult(
    string IpAddress,
    bool IsAlive,
    long? RoundTripMs,
    int? Ttl,
    string? ErrorMessage = null
);

/// <summary>
/// Result of a TCP port scan.
/// </summary>
public record PortScanResult(
    string IpAddress,
    int Port,
    bool IsOpen,
    string? ServiceName,
    string? Banner = null,
    string? ErrorMessage = null
);

/// <summary>
/// NetBIOS information from a Windows device.
/// </summary>
public record NetBiosInfo(
    string? ComputerName,
    string? DomainName,
    string? UserName,
    string? MacAddress,
    int? MajorVersion,
    int? MinorVersion
);

/// <summary>
/// OS fingerprint information from TTL analysis and banner grabbing.
/// </summary>
public record OsFingerprint(
    string? OsGuess,
    string? OsVersion,
    int? Ttl,
    Dictionary<int, string> Banners,
    string? Hostname
);
