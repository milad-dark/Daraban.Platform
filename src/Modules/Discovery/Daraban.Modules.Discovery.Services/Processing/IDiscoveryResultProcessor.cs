namespace Daraban.Modules.Discovery.Services.Processing;

/// <summary>
/// Processes discovery scan results (Task 5.4).
/// Handles asset matching, auto-creation, and offline detection.
/// </summary>
public interface IDiscoveryResultProcessor
{
    /// <summary>
    /// Process a completed scan's results.
    /// Matches discovered devices against existing assets and applies discovery rules.
    /// </summary>
    Task<DiscoveryResultSummary> ProcessScanResultsAsync(Guid scanId, CancellationToken ct = default);

    /// <summary>
    /// Match a discovered device against existing assets by MAC address.
    /// </summary>
    Task<AssetMatchResult> MatchDeviceToAssetAsync(DeviceResponse device, CancellationToken ct = default);

    /// <summary>
    /// Apply discovery rules to a device and create/update assets as needed.
    /// </summary>
    Task<List<AssetAction>> ApplyDiscoveryRulesAsync(DeviceResponse device, CancellationToken ct = default);

    /// <summary>
    /// Check for devices that haven't been seen in recent scans (offline detection).
    /// </summary>
    Task<List<OfflineDeviceAlert>> DetectOfflineDevicesAsync(Guid rangeId, TimeSpan threshold, CancellationToken ct = default);
}

/// <summary>
/// Summary of processing results for a scan.
/// </summary>
public record DiscoveryResultSummary(
    int TotalDevices,
    int NewDevices,
    int UpdatedDevices,
    int AssetsCreated,
    int AssetsUpdated,
    int OfflineDevices,
    List<AssetAction> Actions
);

/// <summary>
/// Result of matching a device to an asset.
/// </summary>
public record AssetMatchResult(
    bool Found,
    Guid? AssetId,
    string? MatchedBy,
    string? MatchValue
);

/// <summary>
/// Action taken on an asset during discovery processing.
/// </summary>
public record AssetAction(
    AssetActionType ActionType,
    Guid? AssetId,
    string DeviceIp,
    string? AssetName,
    string? Reason
);

/// <summary>
/// Types of asset actions.
/// </summary>
public enum AssetActionType
{
    Created,
    Updated,
    Skipped,
    Matched
}

/// <summary>
/// Alert for an offline device.
/// </summary>
public record OfflineDeviceAlert(
    long DeviceId,
    string IpAddress,
    string? Hostname,
    DateTimeOffset LastSeenAt,
    TimeSpan OfflineDuration
);
