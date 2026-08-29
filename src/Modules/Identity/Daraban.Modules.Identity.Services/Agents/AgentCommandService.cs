using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Platform.Contracts.Agents;

namespace Daraban.Modules.Identity.Services.Agents;

/// <summary>
/// Implements the remote command lifecycle (Task 4.4):
/// Created → Queued → Dispatched → Received → Executing → Completed/Failed/TimedOut.
///
/// Timeout defaults per command type (seconds):
///   RunScript:           300 (5 min)
///   InstallSoftware:     600 (10 min)
///   UninstallSoftware:   600 (10 min)
///   RestartService:      120 (2 min)
///   RebootDevice:        300 (5 min)
///   CollectInventoryNow: 600 (10 min)
/// </summary>
public class AgentCommandService(IAgentCommandRepository repo) : IAgentCommandService
{
    public async Task<CommandDto> CreateCommandAsync(CreateCommandRequest request, CancellationToken ct = default)
    {
        var timeout = request.TimeoutSeconds ?? GetDefaultTimeout(request.CommandType);
        var command = new AgentCommand
        {
            Id = Guid.NewGuid(),
            AgentId = request.AgentId,
            CommandType = request.CommandType,
            Status = CommandStatus.Queued,
            Payload = request.Payload,
            TimeoutSeconds = timeout,
            MaxRetries = request.MaxRetries,
            RetryCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        repo.AddCommand(command);
        await repo.SaveChangesAsync(ct);

        return MapToDto(command);
    }

    public async Task<IReadOnlyList<PendingCommandDto>> GetPendingCommandsAsync(
        Guid agentId, int maxCount = 10, CancellationToken ct = default)
    {
        var commands = await repo.GetPendingCommandsAsync(agentId, maxCount, ct);
        return commands.Select(c => new PendingCommandDto(
            c.Id, c.CommandType, c.Payload, c.TimeoutSeconds, c.CreatedAt)).ToList();
    }

    public async Task<bool> AcknowledgeCommandAsync(Guid agentId, Guid commandId, CancellationToken ct = default)
    {
        var command = await repo.GetCommandByIdAsync(commandId, ct);
        if (command is null || command.AgentId != agentId)
            return false;

        // Only acknowledge if in Queued or Dispatched state
        if (command.Status != CommandStatus.Queued && command.Status != CommandStatus.Dispatched)
            return false;

        command.Status = CommandStatus.Received;
        command.ReceivedAt = DateTimeOffset.UtcNow;
        command.UpdatedAt = DateTimeOffset.UtcNow;

        await repo.SaveChangesAsync(ct);
        return true;
    }

    public async Task<CommandResultResponse> ReportResultAsync(
        Guid agentId, Guid commandId, CommandResultRequest request, CancellationToken ct = default)
    {
        var command = await repo.GetCommandByIdAsync(commandId, ct);
        if (command is null || command.AgentId != agentId)
            throw new InvalidOperationException($"Command {commandId} not found or not owned by agent {agentId}.");

        // Transition to terminal state
        command.Status = request.Success ? CommandStatus.Completed : CommandStatus.Failed;
        command.ExitCode = request.ExitCode;
        command.CompletedAt = DateTimeOffset.UtcNow;
        command.UpdatedAt = DateTimeOffset.UtcNow;

        if (!request.Success)
            command.LastError = request.ErrorMessage;

        // Store the full result
        var result = new CommandResult
        {
            CommandId = commandId,
            AgentId = agentId,
            Success = request.Success,
            Output = request.Output,
            ErrorMessage = request.ErrorMessage,
            ExitCode = request.ExitCode,
            ExecutionDurationMs = request.ExecutionDurationMs,
            ReceivedAt = DateTimeOffset.UtcNow,
        };

        repo.AddResult(result);
        await repo.SaveChangesAsync(ct);

        return new CommandResultResponse(commandId, command.Status, result.ReceivedAt);
    }

    public async Task MarkDispatchedAsync(Guid commandId, CancellationToken ct = default)
    {
        var command = await repo.GetCommandByIdAsync(commandId, ct);
        if (command is null)
            return;

        command.Status = CommandStatus.Dispatched;
        command.DispatchedAt = DateTimeOffset.UtcNow;
        command.UpdatedAt = DateTimeOffset.UtcNow;

        // Set deadline based on timeout
        if (command.TimeoutSeconds is > 0)
            command.DeadlineAt = DateTimeOffset.UtcNow.AddSeconds(command.TimeoutSeconds.Value);

        await repo.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CommandDto>> ProcessTimedOutCommandsAsync(CancellationToken ct = default)
    {
        var timedOut = await repo.GetTimedOutCommandsAsync(DateTimeOffset.UtcNow, 50, ct);
        var failedCommands = new List<CommandDto>();

        foreach (var command in timedOut)
        {
            if (command.RetryCount < command.MaxRetries)
            {
                // Re-queue for retry
                command.Status = CommandStatus.Queued;
                command.RetryCount++;
                command.DispatchedAt = null;
                command.ReceivedAt = null;
                command.DeadlineAt = null;
                command.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                // Max retries exceeded — permanently failed
                command.Status = CommandStatus.Failed;
                command.LastError = $"Timed out after {command.TimeoutSeconds}s with {command.RetryCount} retries.";
                command.CompletedAt = DateTimeOffset.UtcNow;
                command.UpdatedAt = DateTimeOffset.UtcNow;
                failedCommands.Add(MapToDto(command));
            }
        }

        if (timedOut.Count > 0)
            await repo.SaveChangesAsync(ct);

        return failedCommands;
    }

    public async Task<CommandDto?> GetCommandAsync(Guid commandId, CancellationToken ct = default)
    {
        var command = await repo.GetCommandByIdAsync(commandId, ct);
        return command is null ? null : MapToDto(command);
    }

    public async Task<(IReadOnlyList<CommandDto> Items, int TotalCount)> GetCommandsByAgentAsync(
        Guid agentId, int page, int pageSize, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var items = await repo.GetCommandsByAgentAsync(agentId, skip, pageSize, ct);
        var total = await repo.GetCommandCountByAgentAsync(agentId, ct);
        return (items.Select(MapToDto).ToList(), total);
    }

    // ---- Helpers ----

    private static CommandDto MapToDto(AgentCommand c) => new(
        c.Id, c.AgentId, c.CommandType, c.Status, c.Payload,
        c.TimeoutSeconds, c.RetryCount, c.MaxRetries, c.LastError,
        c.CreatedAt, c.UpdatedAt, c.DispatchedAt, c.CompletedAt);

    private static int GetDefaultTimeout(CommandType type) => type switch
    {
        CommandType.RunScript => 300,
        CommandType.InstallSoftware => 600,
        CommandType.UninstallSoftware => 600,
        CommandType.RestartService => 120,
        CommandType.RebootDevice => 300,
        CommandType.CollectInventoryNow => 600,
        _ => 300,
    };
}
