namespace Daraban.Modules.Discovery.Data.Entities;

/// <summary>
/// Records a single scan execution against a discovery range (Task 5.1).
/// Tracks lifecycle from queued → running → completed/failed.
/// </summary>
public class DiscoveryScan
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The range that was scanned.</summary>
    public Guid RangeId { get; set; }

    /// <summary>Scan status.</summary>
    public ScanStatus Status { get; set; } = ScanStatus.Queued;

    /// <summary>Scan type used for this execution.</summary>
    public ScanType ScanType { get; set; } = ScanType.Ping;

    /// <summary>Number of devices found during this scan.</summary>
    public int DevicesFound { get; set; } = 0;

    /// <summary>Number of IPs responded to ping.</summary>
    public int IpsResponded { get; set; } = 0;

    /// <summary>Total IPs in the range.</summary>
    public int TotalIps { get; set; } = 0;

    /// <summary>When the scan was queued.</summary>
    public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the scan actually started.</summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>When the scan completed or failed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Duration of the scan (null if still running).</summary>
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    /// <summary>Error message if scan failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Scan log entries (JSON array of timestamped messages).</summary>
    public string? ScanLog { get; set; }

    /// <summary>User who initiated the scan (null for scheduled scans).</summary>
    public string? InitiatedBy { get; set; }

    /// <summary>Navigation: the range scanned.</summary>
    public DiscoveryRange Range { get; set; } = default!;

    /// <summary>Navigation: devices discovered during this scan.</summary>
    public ICollection<DiscoveredDevice> Devices { get; set; } = new List<DiscoveredDevice>();
}

/// <summary>Scan status enumeration.</summary>
public enum ScanStatus
{
    /// <summary>Queued, waiting to start.</summary>
    Queued = 0,

    /// <summary>Currently running.</summary>
    Running = 1,

    /// <summary>Completed successfully.</summary>
    Completed = 2,

    /// <summary>Failed (see ErrorMessage).</summary>
    Failed = 3,

    /// <summary>Cancelled by user.</summary>
    Cancelled = 4,

    /// <summary>Partially completed (some IPs timed out).</summary>
    Partial = 5,
}
