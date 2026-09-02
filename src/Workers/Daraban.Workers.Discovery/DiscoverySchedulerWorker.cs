using Daraban.Modules.Discovery.Services;
using Daraban.Modules.Discovery.Services.Scheduling;

namespace Daraban.Workers.Discovery;

/// <summary>
/// Background service that checks for scheduled scans and triggers them (Task 5.4).
/// Runs every minute to check if any ranges are due for scanning.
/// </summary>
public class DiscoverySchedulerWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DiscoverySchedulerWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public DiscoverySchedulerWorker(
        IServiceProvider serviceProvider,
        ILogger<DiscoverySchedulerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DiscoverySchedulerWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndTriggerScheduledScansAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking scheduled scans");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("DiscoverySchedulerWorker stopped");
    }

    private async Task CheckAndTriggerScheduledScansAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var scheduler = scope.ServiceProvider.GetRequiredService<IDiscoveryScheduler>();
        var discoveryService = scope.ServiceProvider.GetRequiredService<IDiscoveryService>();

        // Get all ranges due for scanning
        var dueRanges = await scheduler.GetRangesDueForScanAsync(ct);

        foreach (var rangeInfo in dueRanges)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                _logger.LogInformation(
                    "Range {RangeName} ({RangeId}) is overdue for scan (last: {LastScanAt}, next: {NextScanAt})",
                    rangeInfo.RangeName, rangeInfo.RangeId, rangeInfo.LastScanAt, rangeInfo.NextScanAt);

                // Trigger the scan
                var scan = await scheduler.TriggerImmediateScanAsync(
                    rangeInfo.RangeId,
                    "Scheduler",
                    ct);

                // Update last scan time
                await scheduler.UpdateLastScanTimeAsync(rangeInfo.RangeId, DateTimeOffset.UtcNow, ct);

                _logger.LogInformation(
                    "Triggered scheduled scan {ScanId} for range {RangeName}",
                    scan.Id, rangeInfo.RangeName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to trigger scheduled scan for range {RangeId}",
                    rangeInfo.RangeId);
            }
        }
    }
}
