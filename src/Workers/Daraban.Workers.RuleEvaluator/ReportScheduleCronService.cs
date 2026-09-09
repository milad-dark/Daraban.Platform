using Daraban.Modules.Reporting.Data.Entities;
using Daraban.Modules.Reporting.Data.Repositories;
using Daraban.Modules.Reporting.Services.Reports;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Contracts.Reporting;

namespace Daraban.Workers.RuleEvaluator;

/// <summary>
/// Scheduled-report runner (Task 7.2): the Automation-side cron half of report generation.
/// Ticks once per minute, finds schedule-enabled definitions whose cron expression matches
/// the current minute, inserts a Pending SavedReport, and publishes ReportRequestedEvent for
/// Worker.Reporting -- exactly the same queue path a manual run takes, so the worker and the
/// file store only ever see one message shape.
///
/// Fires are de-duplicated in memory (definition id + minute); on restart a definition fires
/// again only if its minute is still current, which is the right trade-off for minute-granularity
/// reports and avoids persisting scheduler state. Cron semantics come from the shared
/// CronSchedule in the Reporting module -- the same parser that validated the definition,
/// so an accepted schedule can never fail to fire.
/// </summary>
public class ReportScheduleCronService(
    IServiceScopeFactory scopeFactory,
    IEventPublisher eventPublisher,
    ILogger<ReportScheduleCronService> logger) : BackgroundService
{
    private readonly HashSet<(Guid DefinitionId, long Minute)> _fired = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Align the first tick to the next minute boundary so schedule semantics are exact.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One bad tick (e.g. DB blip) must not kill the scheduler.
                logger.LogError(ex, "Report schedule tick failed -- retrying next minute");
            }

            TrimFiredLog();
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var definitions = scope.ServiceProvider.GetRequiredService<IReportDefinitionRepository>();
        var savedReports = scope.ServiceProvider.GetRequiredService<ISavedReportRepository>();

        var scheduled = await definitions.ListScheduledAsync(ct);
        if (scheduled.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        var currentMinute = now.ToUnixTimeSeconds() / 60;

        foreach (var definition in scheduled)
        {
            if (definition.Schedule is null) continue;
            if (!CronSchedule.TryParse(definition.Schedule, out var schedule) || schedule is null)
            {
                // The validator rejects unparsable expressions at creation; log and skip so a
                // hand-edited row cannot crash the scheduler.
                logger.LogWarning("Schedule-enabled definition {DefinitionId} has unparsable cron '{Cron}' -- skipping",
                    definition.Id, definition.Schedule);
                continue;
            }

            if (!schedule.Matches(now)) continue;
            if (!_fired.Add((definition.Id, currentMinute))) continue; // already fired this minute

            var run = new SavedReport
            {
                DefinitionId = definition.Id,
                EntityId = definition.EntityId,
                RequestedByUserId = definition.OwnerUserId,
                Trigger = ReportTriggers.Schedule,
                Format = definition.Format,
                Status = SavedReportStatus.Pending,
                CreatedById = definition.OwnerUserId,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await savedReports.AddAsync(run, ct);
            await savedReports.SaveChangesAsync(ct);

            await eventPublisher.PublishAsync(new ReportRequestedEvent(
                run.Id, definition.Id, definition.EntityId, definition.OwnerUserId, ReportTriggers.Schedule), ct);

            logger.LogInformation("Scheduled report fired: definition {DefinitionId} -> saved report {SavedReportId}",
                definition.Id, run.Id);
        }
    }

    /// <summary>Keeps the in-memory de-dupe set bounded -- entries older than one hour are
    /// unreachable because the current minute always advances.</summary>
    private void TrimFiredLog()
    {
        var cutoff = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds() / 60;
        _fired.RemoveWhere(entry => entry.Minute < cutoff);
    }
}
