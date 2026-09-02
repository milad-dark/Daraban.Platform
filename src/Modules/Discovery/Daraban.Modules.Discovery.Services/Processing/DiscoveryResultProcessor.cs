using System.Text.Json;
using Daraban.Modules.Discovery.Data.Entities;
using Daraban.Modules.Discovery.Data.Repositories;
using Daraban.Platform.Abstractions;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Discovery.Services.Processing;

/// <summary>
/// Processes discovery scan results (Task 5.4).
/// Handles asset matching, auto-creation, and offline detection.
/// </summary>
public class DiscoveryResultProcessor : IDiscoveryResultProcessor
{
    private readonly IDiscoveryRepository _repository;
    private readonly IDiscoveryService _discoveryService;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<DiscoveryResultProcessor> _logger;

    public DiscoveryResultProcessor(
        IDiscoveryRepository repository,
        IDiscoveryService discoveryService,
        IEventPublisher eventPublisher,
        ILogger<DiscoveryResultProcessor> logger)
    {
        _repository = repository;
        _discoveryService = discoveryService;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Process a completed scan's results.
    /// </summary>
    public async Task<DiscoveryResultSummary> ProcessScanResultsAsync(Guid scanId, CancellationToken ct = default)
    {
        _logger.LogInformation("Processing results for scan {ScanId}", scanId);

        var devices = await _discoveryService.GetDevicesByScanIdAsync(scanId, ct);
        var actions = new List<AssetAction>();
        int newDevices = 0, updatedDevices = 0, assetsCreated = 0, assetsUpdated = 0;

        foreach (var device in devices)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                // Try to match by MAC address
                var matchResult = await MatchDeviceToAssetAsync(device, ct);

                if (matchResult.Found)
                {
                    // Device already has an asset
                    actions.Add(new AssetAction(
                        AssetActionType.Matched,
                        matchResult.AssetId,
                        device.IpAddress,
                        null,
                        $"Matched by {matchResult.MatchedBy}: {matchResult.MatchValue}"
                    ));
                    updatedDevices++;
                }
                else
                {
                    // Apply discovery rules
                    var ruleActions = await ApplyDiscoveryRulesAsync(device, ct);
                    actions.AddRange(ruleActions);

                    if (ruleActions.Any(a => a.ActionType == AssetActionType.Created))
                    {
                        assetsCreated++;
                        newDevices++;
                    }
                    else if (ruleActions.Any(a => a.ActionType == AssetActionType.Updated))
                    {
                        assetsUpdated++;
                        updatedDevices++;
                    }
                    else
                    {
                        newDevices++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process device {DeviceId} at {IpAddress}",
                    device.Id, device.IpAddress);
                actions.Add(new AssetAction(
                    AssetActionType.Skipped,
                    null,
                    device.IpAddress,
                    null,
                    $"Error: {ex.Message}"
                ));
            }
        }

        // Check for offline devices in this range
        var rangeId = devices.FirstOrDefault()?.RangeId;
        var offlineDevices = 0;
        if (rangeId.HasValue)
        {
            var offlineAlerts = await DetectOfflineDevicesAsync(
                rangeId.Value, TimeSpan.FromHours(24), ct);
            offlineDevices = offlineAlerts.Count;
        }

        var summary = new DiscoveryResultSummary(
            TotalDevices: devices.Count,
            NewDevices: newDevices,
            UpdatedDevices: updatedDevices,
            AssetsCreated: assetsCreated,
            AssetsUpdated: assetsUpdated,
            OfflineDevices: offlineDevices,
            Actions: actions
        );

        // Publish DiscoveryCompleted event
        await _eventPublisher.PublishAsync(new DiscoveryCompletedEvent(
            ScanId: scanId,
            TotalDevices: summary.TotalDevices,
            NewDevices: summary.NewDevices,
            AssetsCreated: summary.AssetsCreated,
            OfflineDevices: summary.OfflineDevices
        ), ct);

        _logger.LogInformation(
            "Scan {ScanId} processing complete: {Total} devices, {New} new, {Assets} assets created",
            scanId, summary.TotalDevices, summary.NewDevices, summary.AssetsCreated);

        return summary;
    }

    /// <summary>
    /// Match a discovered device against existing assets by MAC address.
    /// </summary>
    public async Task<AssetMatchResult> MatchDeviceToAssetAsync(DeviceResponse device, CancellationToken ct = default)
    {
        // Primary match: MAC address
        if (!string.IsNullOrEmpty(device.MacAddress))
        {
            var existingDevice = await _repository.GetDeviceByMacAddressAsync(device.MacAddress, ct);
            if (existingDevice?.AssetId.HasValue == true)
            {
                return new AssetMatchResult(
                    Found: true,
                    AssetId: existingDevice.AssetId,
                    MatchedBy: "MAC Address",
                    MatchValue: device.MacAddress
                );
            }
        }

        // Secondary match: hostname + IP
        if (!string.IsNullOrEmpty(device.Hostname))
        {
            var existingDevice = await _repository.GetDeviceByHostnameAsync(device.Hostname, ct);
            if (existingDevice?.AssetId.HasValue == true)
            {
                return new AssetMatchResult(
                    Found: true,
                    AssetId: existingDevice.AssetId,
                    MatchedBy: "Hostname",
                    MatchValue: device.Hostname
                );
            }
        }

        return new AssetMatchResult(false, null, null, null);
    }

    /// <summary>
    /// Apply discovery rules to a device.
    /// </summary>
    public async Task<List<AssetAction>> ApplyDiscoveryRulesAsync(DeviceResponse device, CancellationToken ct = default)
    {
        var actions = new List<AssetAction>();
        var rules = await _discoveryService.GetActiveRulesAsync(ct);

        foreach (var rule in rules.OrderBy(r => r.Priority))
        {
            if (MatchesFilterCriteria(device, rule.FilterCriteria))
            {
                _logger.LogInformation(
                    "Device {IpAddress} matches rule {RuleName}",
                    device.IpAddress, rule.Name);

                switch (rule.Action)
                {
                    case MatchAction.CreateAsset:
                        actions.Add(new AssetAction(
                            AssetActionType.Created,
                            null,
                            device.IpAddress,
                            device.Hostname ?? device.IpAddress,
                            $"Created by rule: {rule.Name}"
                        ));
                        break;

                    case MatchAction.UpdateAsset:
                        actions.Add(new AssetAction(
                            AssetActionType.Updated,
                            device.AssetId,
                            device.IpAddress,
                            device.Hostname ?? device.IpAddress,
                            $"Updated by rule: {rule.Name}"
                        ));
                        break;

                    case MatchAction.Ignore:
                        actions.Add(new AssetAction(
                            AssetActionType.Skipped,
                            null,
                            device.IpAddress,
                            null,
                            $"Ignored by rule: {rule.Name}"
                        ));
                        break;

                    case MatchAction.CreateAndAssign:
                        actions.Add(new AssetAction(
                            AssetActionType.Created,
                            null,
                            device.IpAddress,
                            device.Hostname ?? device.IpAddress,
                            $"Created and assigned by rule: {rule.Name}"
                        ));
                        break;
                }
            }
        }

        return actions;
    }

    /// <summary>
    /// Check for devices that haven't been seen recently.
    /// </summary>
    public async Task<List<OfflineDeviceAlert>> DetectOfflineDevicesAsync(
        Guid rangeId,
        TimeSpan threshold,
        CancellationToken ct = default)
    {
        var devices = await _discoveryService.GetDevicesByRangeIdAsync(rangeId, ct);
        var now = DateTimeOffset.UtcNow;
        var alerts = new List<OfflineDeviceAlert>();

        foreach (var device in devices)
        {
            if (device.LastSeenAt.HasValue)
            {
                var offlineDuration = now - device.LastSeenAt.Value;
                if (offlineDuration > threshold)
                {
                    alerts.Add(new OfflineDeviceAlert(
                        DeviceId: device.Id,
                        IpAddress: device.IpAddress,
                        Hostname: device.Hostname,
                        LastSeenAt: device.LastSeenAt.Value,
                        OfflineDuration: offlineDuration
                    ));
                }
            }
        }

        return alerts;
    }

    private bool MatchesFilterCriteria(DeviceResponse device, string filterCriteriaJson)
    {
        try
        {
            var criteria = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(filterCriteriaJson);
            if (criteria == null) return false;

            // Check OsGuess
            if (criteria.TryGetValue("OsGuess", out var osGuess) &&
                !string.IsNullOrEmpty(device.OsGuess))
            {
                var pattern = osGuess.GetString();
                if (!string.IsNullOrEmpty(pattern) &&
                    !device.OsGuess.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Check Vendor
            if (criteria.TryGetValue("Vendor", out var vendor) &&
                !string.IsNullOrEmpty(device.Vendor))
            {
                var pattern = vendor.GetString();
                if (!string.IsNullOrEmpty(pattern) &&
                    !device.Vendor.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Check OpenPorts (any match)
            if (criteria.TryGetValue("OpenPorts", out var portsElement) &&
                !string.IsNullOrEmpty(device.OpenPorts))
            {
                var requiredPorts = portsElement.EnumerateArray()
                    .Select(p => p.GetInt32())
                    .ToList();
                var devicePorts = device.OpenPorts.Split(',')
                    .Select(p => int.TryParse(p.Trim(), out var port) ? port : -1)
                    .Where(p => p > 0)
                    .ToList();

                if (!requiredPorts.Any(rp => devicePorts.Contains(rp)))
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse filter criteria: {Criteria}", filterCriteriaJson);
            return false;
        }
    }
}

/// <summary>
/// Event published when a discovery scan completes.
/// </summary>
public record DiscoveryCompletedEvent(
    Guid ScanId,
    int TotalDevices,
    int NewDevices,
    int AssetsCreated,
    int OfflineDevices
);
