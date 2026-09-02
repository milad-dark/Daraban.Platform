using Daraban.Modules.Discovery.Data.Entities;
using Daraban.Modules.Discovery.Services;

namespace Daraban.Modules.Discovery.Services.Scheduling;

/// <summary>
/// Manages discovery scan scheduling (Task 5.4).
/// Supports cron-based schedules and immediate scan triggers.
/// </summary>
public interface IDiscoveryScheduler
{
    /// <summary>
    /// Check if a range is due for scanning based on its schedule.
    /// </summary>
    Task<bool> IsRangeDueForScanAsync(Guid rangeId, CancellationToken ct = default);

    /// <summary>
    /// Get all ranges that are due for scanning.
    /// </summary>
    Task<List<RangeScheduleInfo>> GetRangesDueForScanAsync(CancellationToken ct = default);

    /// <summary>
    /// Update the last scan time for a range.
    /// </summary>
    Task UpdateLastScanTimeAsync(Guid rangeId, DateTimeOffset scanTime, CancellationToken ct = default);

    /// <summary>
    /// Calculate the next scan time for a range based on its interval.
    /// </summary>
    DateTimeOffset? CalculateNextScanTime(DiscoveryRange range);

    /// <summary>
    /// Trigger an immediate scan for a range (bypasses schedule).
    /// </summary>
    Task<ScanResponse> TriggerImmediateScanAsync(Guid rangeId, string? initiatedBy, CancellationToken ct = default);
}

/// <summary>
/// Information about a range's scheduling status.
/// </summary>
public record RangeScheduleInfo(
    Guid RangeId,
    string RangeName,
    int IntervalHours,
    DateTimeOffset? LastScanAt,
    DateTimeOffset? NextScanAt,
    bool IsOverdue
);
