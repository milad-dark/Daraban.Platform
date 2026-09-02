using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Discovery.Services.Scheduling;

/// <summary>
/// Discovery scan scheduler implementation (Task 5.4).
/// Manages cron-based schedules and immediate scan triggers.
/// </summary>
public class DiscoveryScheduler : IDiscoveryScheduler
{
    private readonly IDiscoveryService _discoveryService;
    private readonly ILogger<DiscoveryScheduler> _logger;

    public DiscoveryScheduler(IDiscoveryService discoveryService, ILogger<DiscoveryScheduler> logger)
    {
        _discoveryService = discoveryService;
        _logger = logger;
    }

    /// <summary>
    /// Check if a range is due for scanning based on its interval.
    /// </summary>
    public async Task<bool> IsRangeDueForScanAsync(Guid rangeId, CancellationToken ct = default)
    {
        var range = await _discoveryService.GetRangeByIdAsync(rangeId, ct);
        if (range == null || !range.IsActive || range.ScanIntervalHours <= 0)
            return false;

        if (range.LastScanAt == null)
            return true; // Never scanned, always due

        var nextScanAt = CalculateNextScanTime(range);
        return nextScanAt.HasValue && nextScanAt.Value <= DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Get all ranges that are due for scanning.
    /// </summary>
    public async Task<List<RangeScheduleInfo>> GetRangesDueForScanAsync(CancellationToken ct = default)
    {
        var activeRanges = await _discoveryService.GetActiveRangesAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var scheduleInfos = activeRanges
            .Where(r => r.ScanIntervalHours > 0) // Only scheduled ranges
            .Select(range =>
            {
                var nextScanAt = CalculateNextScanTime(range);
                var isOverdue = nextScanAt.HasValue && nextScanAt.Value <= now;

                return new RangeScheduleInfo(
                    RangeId: range.Id,
                    RangeName: range.Name,
                    IntervalHours: range.ScanIntervalHours,
                    LastScanAt: range.LastScanAt,
                    NextScanAt: nextScanAt,
                    IsOverdue: isOverdue
                );
            })
            .OrderBy(x => x.NextScanAt ?? DateTimeOffset.MaxValue)
            .ToList();

        return scheduleInfos;
    }

    /// <summary>
    /// Update the last scan time for a range.
    /// </summary>
    public async Task UpdateLastScanTimeAsync(Guid rangeId, DateTimeOffset scanTime, CancellationToken ct = default)
    {
        await _discoveryService.UpdateRangeLastScanTimeAsync(rangeId, scanTime, ct);
        _logger.LogInformation("Updated last scan time for range {RangeId} to {ScanTime}", rangeId, scanTime);
    }

    /// <summary>
    /// Calculate the next scan time based on the range's interval.
    /// </summary>
    public DateTimeOffset? CalculateNextScanTime(DiscoveryRange range)
    {
        if (range.ScanIntervalHours <= 0)
            return null; // Manual only

        var baseTime = range.LastScanAt ?? range.CreatedAt;
        return baseTime.AddHours(range.ScanIntervalHours);
    }

    /// <summary>
    /// Calculate the next scan time based on the range response.
    /// </summary>
    public DateTimeOffset? CalculateNextScanTime(RangeResponse range)
    {
        if (range.ScanIntervalHours <= 0)
            return null; // Manual only

        var baseTime = range.LastScanAt ?? range.CreatedAt;
        return baseTime.AddHours(range.ScanIntervalHours);
    }

    /// <summary>
    /// Trigger an immediate scan for a range (bypasses schedule).
    /// </summary>
    public async Task<ScanResponse> TriggerImmediateScanAsync(
        Guid rangeId,
        string? initiatedBy,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Triggering immediate scan for range {RangeId} by {InitiatedBy}",
            rangeId, initiatedBy);

        var scan = await _discoveryService.StartScanAsync(
            new StartScanRequest(rangeId),
            initiatedBy,
            ct);

        _logger.LogInformation(
            "Immediate scan {ScanId} queued for range {RangeId}",
            scan.Id, rangeId);

        return scan;
    }
}
