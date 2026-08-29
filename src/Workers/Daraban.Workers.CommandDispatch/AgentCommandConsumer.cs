using Daraban.Host.AgentApi.Hubs;
using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Contracts.Agents;
using Daraban.Platform.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Daraban.Workers.CommandDispatch;

/// <summary>
/// Consumes AgentCommandPublishedEvent from RabbitMQ and pushes commands to
/// connected agents via SignalR (Task 4.4).
///
/// Flow:
/// 1. Agent or admin creates command → AgentCommandPublishedEvent published to RabbitMQ
/// 2. This consumer receives the event, marks command as Dispatched in DB
/// 3. Pushes command to agent via IHubContext (server-side push, no client connection)
/// 4. If agent is not connected, command stays in Dispatched state
///    and agent will pick it up via HTTP polling (GET /api/agent/commands/pending)
/// </summary>
public class AgentCommandConsumer(
    RabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<AgentCommandConsumer> logger,
    IAgentCommandService commandService,
    IHubContext<AgentControlHub> hubContext) : RabbitMqConsumerBackgroundService<AgentCommandPublishedEvent>(connectionProvider, options, logger)
{
    protected override string QueueName => "command.dispatch";
    protected override string RoutingKey => nameof(AgentCommandPublishedEvent);

    protected override async Task HandleAsync(AgentCommandPublishedEvent message, CancellationToken ct)
    {
        logger.LogInformation(
            "Received command {CommandId} for agent {AgentId} (type={CommandType})",
            message.CommandId, message.AgentId, message.CommandType);

        // Mark as dispatched (sets DeadlineAt based on TimeoutSeconds)
        await commandService.MarkDispatchedAsync(message.CommandId, ct);

        // Push to agent via IHubContext — server-side push, no client connection needed.
        // If the agent is not connected, the message is silently dropped;
        // the agent will retrieve the command via HTTP polling fallback.
        try
        {
            var pendingCommand = new PendingCommandDto(
                message.CommandId,
                Enum.TryParse<CommandType>(message.CommandType, true, out var parsedType)
                    ? parsedType : CommandType.RunScript,
                message.Payload,
                message.TimeoutSeconds,
                message.QueuedAt);

            await hubContext.Clients
                .Group(message.AgentId.ToString())
                .SendAsync("ReceiveCommand", pendingCommand, ct);

            logger.LogInformation(
                "Pushed command {CommandId} to agent {AgentId} via SignalR",
                message.CommandId, message.AgentId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "SignalR push failed for command {CommandId} to agent {AgentId} — " +
                "agent will retrieve via HTTP polling",
                message.CommandId, message.AgentId);
        }
    }
}
