using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Contracts.Agents;

namespace Daraban.Workers.CommandDispatch;

/// <summary>
/// Periodic background service that checks for commands that exceeded their timeout.
/// Runs every 30 seconds. For each timed-out command:
///   - If retries remain: re-queues for retry (status → Queued, retryCount++)
///   - If max retries exceeded: marks as permanently Failed
///
/// Publishes AgentCommandTimedOutEvent for each timed-out command so downstream
/// consumers (notifications, audit) can react.
/// </summary>
public class CommandTimeoutService(
    IAgentCommandService commandService,
    IEventPublisher eventPublisher,
    ILogger<CommandTimeoutService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CommandTimeoutService starting (interval: 30s)");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        do
        {
            try
            {
                var failedCommands = await commandService.ProcessTimedOutCommandsAsync(stoppingToken);

                foreach (var cmd in failedCommands)
                {
                    logger.LogWarning(
                        "Command {CommandId} permanently failed after {Retries}/{MaxRetries} retries " +
                        "(type={CommandType}, agent={AgentId}): {Error}",
                        cmd.Id, cmd.RetryCount, cmd.MaxRetries, cmd.CommandType,
                        cmd.AgentId, cmd.LastError);

                    // Publish event for downstream consumers (notifications, audit)
                    await eventPublisher.PublishAsync(new AgentCommandTimedOutEvent(
                        cmd.Id, cmd.AgentId, cmd.CommandType, DateTimeOffset.UtcNow), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing timed-out commands");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));

        logger.LogInformation("CommandTimeoutService stopping");
    }
}
