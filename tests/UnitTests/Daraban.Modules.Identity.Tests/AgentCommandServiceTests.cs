using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Contracts.Agents;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// AgentCommandService: the remote-command lifecycle
/// (Queued -> Dispatched -> Received -> Completed/Failed/TimedOut) plus retry handling. The
/// ownership checks matter most -- one agent must never be able to acknowledge or report results
/// for another agent's command.
/// </summary>
public class AgentCommandServiceTests
{
    private static readonly Guid AgentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherAgentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IAgentCommandRepository> _repo = new(MockBehavior.Strict);

    private AgentCommandService CreateSut() => new(_repo.Object);

    private static AgentCommand CommandWith(
        CommandStatus status = CommandStatus.Queued,
        Guid? agentId = null,
        int retryCount = 0,
        int maxRetries = 0,
        int? timeoutSeconds = 300)
        => new()
        {
            Id = Guid.CreateVersion7(),
            AgentId = agentId ?? AgentId,
            CommandType = CommandType.RunScript,
            Status = status,
            Payload = "Get-Process",
            TimeoutSeconds = timeoutSeconds,
            RetryCount = retryCount,
            MaxRetries = maxRetries,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    // ---- Create ------------------------------------------------------------------------------

    [Theory]
    [InlineData(CommandType.RunScript, 300)]
    [InlineData(CommandType.InstallSoftware, 600)]
    [InlineData(CommandType.UninstallSoftware, 600)]
    [InlineData(CommandType.RestartService, 120)]
    [InlineData(CommandType.RebootDevice, 300)]
    [InlineData(CommandType.CollectInventoryNow, 600)]
    public async Task CreateCommandAsync_Applies_The_Per_Type_Default_Timeout(
        CommandType type, int expectedTimeout)
    {
        AgentCommand? captured = null;
        _repo.Setup(r => r.AddCommand(It.IsAny<AgentCommand>()))
            .Callback<AgentCommand>(c => captured = c);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().CreateCommandAsync(new CreateCommandRequest(AgentId, type, "payload", null, 0));

        // A software install needs longer than a service restart; a single global default would
        // either time out real installs or let a hung restart sit for ten minutes.
        Assert.Equal(expectedTimeout, captured!.TimeoutSeconds);
    }

    [Fact]
    public async Task CreateCommandAsync_Honours_An_Explicit_Timeout()
    {
        AgentCommand? captured = null;
        _repo.Setup(r => r.AddCommand(It.IsAny<AgentCommand>()))
            .Callback<AgentCommand>(c => captured = c);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().CreateCommandAsync(
            new CreateCommandRequest(AgentId, CommandType.RunScript, "payload", 45, 0));

        Assert.Equal(45, captured!.TimeoutSeconds);
    }

    [Fact]
    public async Task CreateCommandAsync_Starts_Queued_With_No_Retries_Used()
    {
        AgentCommand? captured = null;
        _repo.Setup(r => r.AddCommand(It.IsAny<AgentCommand>()))
            .Callback<AgentCommand>(c => captured = c);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dto = await CreateSut().CreateCommandAsync(
            new CreateCommandRequest(AgentId, CommandType.RunScript, "payload", null, 3));

        Assert.Equal(CommandStatus.Queued, captured!.Status);
        Assert.Equal(0, captured.RetryCount);
        Assert.Equal(3, captured.MaxRetries);
        Assert.Equal(CommandStatus.Queued, dto.Status);

        // No deadline until the command is actually dispatched -- the clock starts when the agent
        // could first have seen it, not when an admin queued it.
        Assert.Null(captured.DeadlineAt);
    }

    // ---- Dispatch ----------------------------------------------------------------------------

    [Fact]
    public async Task MarkDispatchedAsync_Sets_The_Deadline_From_The_Timeout()
    {
        var command = CommandWith(timeoutSeconds: 120);
        _repo.Setup(r => r.GetCommandByIdAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(command);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().MarkDispatchedAsync(command.Id);

        Assert.Equal(CommandStatus.Dispatched, command.Status);
        Assert.NotNull(command.DispatchedAt);
        Assert.NotNull(command.DeadlineAt);
        Assert.InRange(
            command.DeadlineAt!.Value,
            DateTimeOffset.UtcNow.AddSeconds(115),
            DateTimeOffset.UtcNow.AddSeconds(125));
    }

    [Fact]
    public async Task MarkDispatchedAsync_Leaves_The_Deadline_Null_When_There_Is_No_Timeout()
    {
        var command = CommandWith(timeoutSeconds: null);
        _repo.Setup(r => r.GetCommandByIdAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(command);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().MarkDispatchedAsync(command.Id);

        // NULL timeout means "no limit" -- the timeout sweeper must never pick it up.
        Assert.Null(command.DeadlineAt);
    }

    [Fact]
    public async Task MarkDispatchedAsync_Is_Silent_For_An_Unknown_Command()
    {
        var id = Guid.CreateVersion7();
        _repo.Setup(r => r.GetCommandByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AgentCommand?)null);

        await CreateSut().MarkDispatchedAsync(id);

        // Called from the dispatch worker, which must not crash on a command deleted mid-flight.
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Acknowledge -------------------------------------------------------------------------

    [Theory]
    [InlineData(CommandStatus.Queued)]
    [InlineData(CommandStatus.Dispatched)]
    public async Task AcknowledgeCommandAsync_Accepts_Queued_And_Dispatched(CommandStatus from)
    {
        var command = CommandWith(from);
        _repo.Setup(r => r.GetCommandByIdAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(command);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var acknowledged = await CreateSut().AcknowledgeCommandAsync(AgentId, command.Id);

        Assert.True(acknowledged);
        Assert.Equal(CommandStatus.Received, command.Status);
        Assert.NotNull(command.ReceivedAt);
    }

    [Theory]
    [InlineData(CommandStatus.Received)]
    [InlineData(CommandStatus.Executing)]
    [InlineData(CommandStatus.Completed)]
    [InlineData(CommandStatus.Failed)]
    [InlineData(CommandStatus.TimedOut)]
    [InlineData(CommandStatus.Cancelled)]
    public async Task AcknowledgeCommandAsync_Refuses_Any_Later_State(CommandStatus from)
    {
        var command = CommandWith(from);
        _repo.Setup(r => r.GetCommandByIdAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(command);

        var acknowledged = await CreateSut().AcknowledgeCommandAsync(AgentId, command.Id);

        // A double-ack (or an ack after completion) must not rewind the lifecycle.
        Assert.False(acknowledged);
        Assert.Equal(from, command.Status);
    }

    [Fact]
    public async Task AcknowledgeCommandAsync_Refuses_Another_Agents_Command()
    {
        var command = CommandWith(agentId: OtherAgentId);
        _repo.Setup(r => r.GetCommandByIdAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(command);

        var acknowledged = await CreateSut().AcknowledgeCommandAsync(AgentId, command.Id);

        // Command ids are guessable enough that ownership has to be enforced server-side -- an
        // agent must never be able to intercept another agent's work.
        Assert.False(acknowledged);
        Assert.Equal(CommandStatus.Queued, command.Status);
    }

    [Fact]
    public async Task AcknowledgeCommandAsync_Returns_False_For_An_Unknown_Command()
    {
        var id = Guid.CreateVersion7();
        _repo.Setup(r => r.GetCommandByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AgentCommand?)null);

        Assert.False(await CreateSut().AcknowledgeCommandAsync(AgentId, id));
    }

    // ---- Report result -----------------------------------------------------------------------

    [Fact]
    public async Task ReportResultAsync_Marks_A_Successful_Command_Completed()
    {
        var command = CommandWith(CommandStatus.Received);
        CommandResult? capturedResult = null;

        _repo.Setup(r => r.GetCommandByIdAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(command);
        _repo.Setup(r => r.AddResult(It.IsAny<CommandResult>()))
            .Callback<CommandResult>(x => capturedResult = x);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var response = await CreateSut().ReportResultAsync(
            AgentId, command.Id, new CommandResultRequest(true, "done", null, 0, 1234));

        Assert.Equal(CommandStatus.Completed, command.Status);
        Assert.Equal(CommandStatus.Completed, response.Status);
        Assert.NotNull(command.CompletedAt);
        Assert.Equal(0, command.ExitCode);

        // Output lives on the separate result row -- script output can be multi-megabyte and must
        // not bloat the command table.
        Assert.Equal("done", capturedResult!.Output);
        Assert.Equal(1234, capturedResult.ExecutionDurationMs);
    }

    [Fact]
    public async Task ReportResultAsync_Marks_A_Failed_Command_Failed_And_Records_The_Error()
    {
        var command = CommandWith(CommandStatus.Executing);
        _repo.Setup(r => r.GetCommandByIdAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(command);
        _repo.Setup(r => r.AddResult(It.IsAny<CommandResult>()));
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await CreateSut().ReportResultAsync(
            AgentId, command.Id, new CommandResultRequest(false, null, "Access denied", 5, 200));

        Assert.Equal(CommandStatus.Failed, command.Status);
        Assert.Equal("Access denied", command.LastError);
        Assert.Equal(5, command.ExitCode);
    }

    [Fact]
    public async Task ReportResultAsync_Refuses_Another_Agents_Command()
    {
        var command = CommandWith(CommandStatus.Received, agentId: OtherAgentId);
        _repo.Setup(r => r.GetCommandByIdAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(command);

        // Without the ownership check, any agent could forge a result for work it never ran.
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ReportResultAsync(
            AgentId, command.Id, new CommandResultRequest(true, "forged", null, 0, 1)));

        Assert.Equal(CommandStatus.Received, command.Status);
    }

    [Fact]
    public async Task ReportResultAsync_Throws_For_An_Unknown_Command()
    {
        var id = Guid.CreateVersion7();
        _repo.Setup(r => r.GetCommandByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AgentCommand?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().ReportResultAsync(
            AgentId, id, new CommandResultRequest(true, null, null, 0, 1)));
    }

    // ---- Timeout sweep -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessTimedOutCommandsAsync_Requeues_A_Command_With_Retries_Left()
    {
        var command = CommandWith(CommandStatus.Dispatched, retryCount: 0, maxRetries: 2);
        command.DispatchedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        command.ReceivedAt = DateTimeOffset.UtcNow.AddMinutes(-9);
        command.DeadlineAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        _repo.Setup(r => r.GetTimedOutCommandsAsync(It.IsAny<DateTimeOffset>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { command });
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var permanentlyFailed = await CreateSut().ProcessTimedOutCommandsAsync();

        Assert.Equal(CommandStatus.Queued, command.Status);
        Assert.Equal(1, command.RetryCount);

        // The dispatch timestamps must be cleared, otherwise the retry inherits the old deadline
        // and times out again immediately.
        Assert.Null(command.DispatchedAt);
        Assert.Null(command.ReceivedAt);
        Assert.Null(command.DeadlineAt);

        // A requeue is not a failure, so it is not reported to the caller.
        Assert.Empty(permanentlyFailed);
    }

    [Fact]
    public async Task ProcessTimedOutCommandsAsync_Fails_A_Command_That_Has_Exhausted_Its_Retries()
    {
        var command = CommandWith(CommandStatus.Dispatched, retryCount: 2, maxRetries: 2, timeoutSeconds: 300);
        command.DeadlineAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        _repo.Setup(r => r.GetTimedOutCommandsAsync(It.IsAny<DateTimeOffset>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { command });
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var permanentlyFailed = await CreateSut().ProcessTimedOutCommandsAsync();

        Assert.Equal(CommandStatus.Failed, command.Status);
        Assert.NotNull(command.CompletedAt);
        Assert.Contains("Timed out", command.LastError);

        // Returned so the worker can notify an admin -- a silently failed command is worse than a
        // loud one.
        var reported = Assert.Single(permanentlyFailed);
        Assert.Equal(command.Id, reported.Id);
    }

    [Fact]
    public async Task ProcessTimedOutCommandsAsync_Handles_A_Mixed_Batch()
    {
        var retryable = CommandWith(CommandStatus.Dispatched, retryCount: 0, maxRetries: 1);
        var exhausted = CommandWith(CommandStatus.Dispatched, retryCount: 1, maxRetries: 1);

        _repo.Setup(r => r.GetTimedOutCommandsAsync(It.IsAny<DateTimeOffset>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { retryable, exhausted });
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var permanentlyFailed = await CreateSut().ProcessTimedOutCommandsAsync();

        Assert.Equal(CommandStatus.Queued, retryable.Status);
        Assert.Equal(CommandStatus.Failed, exhausted.Status);
        Assert.Single(permanentlyFailed);
    }

    [Fact]
    public async Task ProcessTimedOutCommandsAsync_Does_Not_Save_When_Nothing_Timed_Out()
    {
        _repo.Setup(r => r.GetTimedOutCommandsAsync(It.IsAny<DateTimeOffset>(), 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AgentCommand>());

        var result = await CreateSut().ProcessTimedOutCommandsAsync();

        // This runs on a timer against every agent -- an unconditional SaveChanges would be pure
        // write load on an idle fleet.
        Assert.Empty(result);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Reads -------------------------------------------------------------------------------

    [Fact]
    public async Task GetPendingCommandsAsync_Projects_Only_What_The_Agent_Needs()
    {
        var command = CommandWith();
        _repo.Setup(r => r.GetPendingCommandsAsync(AgentId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { command });

        var pending = await CreateSut().GetPendingCommandsAsync(AgentId);

        var dto = Assert.Single(pending);
        Assert.Equal(command.Id, dto.CommandId);
        Assert.Equal(command.CommandType, dto.CommandType);
        Assert.Equal(command.Payload, dto.Payload);
        Assert.Equal(command.TimeoutSeconds, dto.TimeoutSeconds);
    }

    [Fact]
    public async Task GetCommandAsync_Returns_Null_For_An_Unknown_Command()
    {
        var id = Guid.CreateVersion7();
        _repo.Setup(r => r.GetCommandByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AgentCommand?)null);

        Assert.Null(await CreateSut().GetCommandAsync(id));
    }

    [Fact]
    public async Task GetCommandsByAgentAsync_Translates_Page_Numbers_To_A_Skip()
    {
        _repo.Setup(r => r.GetCommandsByAgentAsync(AgentId, 40, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { CommandWith() });
        _repo.Setup(r => r.GetCommandCountByAgentAsync(AgentId, It.IsAny<CancellationToken>())).ReturnsAsync(101);

        var (items, total) = await CreateSut().GetCommandsByAgentAsync(AgentId, page: 3, pageSize: 20);

        // Page 3 of 20 => skip 40, not 60.
        Assert.Single(items);
        Assert.Equal(101, total);
        _repo.Verify(r => r.GetCommandsByAgentAsync(AgentId, 40, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
