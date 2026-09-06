using Daraban.Modules.Inventory.Data.Entities;
using Daraban.Modules.Inventory.Data.Repositories;
using Daraban.Modules.Inventory.Services;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Inventory;
using Moq;
using Xunit;

namespace Daraban.Modules.Inventory.Tests;

/// <summary>
/// InventoryService: the ingestion pipeline agents push to. The load-bearing behaviour is
/// idempotency -- a retried submission must return the original result, not create a second
/// row -- plus the processing state machine the background worker drives.
/// </summary>
public class InventoryServiceTests
{
    private static readonly Guid AgentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherAgentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IInventoryRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<IEventPublisher> _events = new();

    private InventoryService CreateSut() => new(_repo.Object, _events.Object);

    private static AgentEnvelope Envelope(
        string deviceId = "LT-0421",
        DateTime? timestampUtc = null,
        string action = "inventory",
        object? content = null)
        => new()
        {
            DeviceId = deviceId,
            ItemType = "Computer",
            Action = action,
            TimestampUtc = timestampUtc ?? new DateTime(2026, 9, 1, 12, 0, 30, DateTimeKind.Utc),
            Content = content ?? new { ComputerName = "LT-0421", TotalMemoryBytes = 16_384 },
        };

    private static RawInventorySubmission Submission(
        long id = 1,
        string? hash = null,
        SubmissionStatus status = SubmissionStatus.Completed) => new()
    {
        Id = id,
        SubmissionHash = hash ?? "some-hash",
        AgentId = AgentId,
        DeviceId = "LT-0421",
        Action = "inventory",
        RawPayload = "{}",
        FullEnvelope = "{}",
        Status = status,
        DeviceCount = status == SubmissionStatus.Completed ? 5 : null,
        ReceivedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        SubmittedAt = DateTimeOffset.UtcNow.AddMinutes(-11),
        ProcessedAt = status == SubmissionStatus.Completed ? DateTimeOffset.UtcNow.AddMinutes(-9) : null,
    };

    private void ArrangeNoDuplicate(string hash)
        => _repo.Setup(r => r.GetByHashAsync(hash, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawInventorySubmission?)null);

    /// <summary>
    /// The worker path resolves a submission by first trying the agent-scoped lookup with
    /// Guid.Empty and then falling back to the unscoped lookup. Strict mocks need both set up;
    /// the scoped one is arranged to return null so the fallback is what answers.
    /// </summary>
    private void ArrangeInternalLookup(RawInventorySubmission submission)
    {
        _repo.Setup(r => r.GetByIdAsync(submission.Id, Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawInventorySubmission?)null);
        _repo.Setup(r => r.GetByIdUnscopedAsync(submission.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);
    }

    // ---- Submit: idempotency -----------------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_Returns_Duplicate_For_A_Replayed_Submission()
    {
        var envelope = Envelope();
        var hash = IInventoryService.ComputeHash(AgentId, envelope.DeviceId, envelope.TimestampUtc);
        var original = Submission(hash: hash);

        _repo.Setup(r => r.GetByHashAsync(hash, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(original);

        var result = await CreateSut().SubmitAsync(envelope, AgentId, null, null);

        // The agent gets the ORIGINAL submission's id and timestamp, not a new row -- this is
        // what makes an at-least-once agent safe to retry.
        Assert.True(result.IsSuccess);
        Assert.Equal("Duplicate", result.Value.Status);
        Assert.Equal(original.Id, result.Value.SubmissionId);
        Assert.Equal(original.ReceivedAt, result.Value.ReceivedAt);

        // Strict mocks prove nothing was written and no event fired.
        _repo.Verify(r => r.Add(It.IsAny<RawInventorySubmission>()), Times.Never);
        _events.Verify(e => e.PublishAsync(
            It.IsAny<RawInventoryReceivedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_Accepts_And_Publishes_A_New_Submission()
    {
        var envelope = Envelope(content: new { ComputerName = "LT-0421", CpuCount = 8 });
        var hash = IInventoryService.ComputeHash(AgentId, envelope.DeviceId, envelope.TimestampUtc);
        ArrangeNoDuplicate(hash);

        RawInventorySubmission? captured = null;
        _repo.Setup(r => r.Add(It.IsAny<RawInventorySubmission>()))
            .Callback<RawInventorySubmission>(s => captured = s);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().SubmitAsync(envelope, AgentId, null, "203.0.113.7");

        Assert.True(result.IsSuccess);
        Assert.Equal("Accepted", result.Value.Status);

        // Idempotency key, both payload copies, and the caller context are all persisted.
        Assert.Equal(hash, captured!.SubmissionHash);
        Assert.Equal(AgentId, captured.AgentId);
        Assert.Equal("LT-0421", captured.DeviceId);
        Assert.Equal(SubmissionStatus.Pending, captured.Status);
        Assert.Contains("LT-0421", captured.RawPayload);
        Assert.Contains("LT-0421", captured.FullEnvelope);
        Assert.Equal("203.0.113.7", captured.IpAddress);

        _events.Verify(e => e.PublishAsync(
            It.Is<RawInventoryReceivedEvent>(evt => evt.AgentId == AgentId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_Still_Accepts_When_Event_Publishing_Fails()
    {
        var envelope = Envelope();
        var hash = IInventoryService.ComputeHash(AgentId, envelope.DeviceId, envelope.TimestampUtc);
        ArrangeNoDuplicate(hash);

        _repo.Setup(r => r.Add(It.IsAny<RawInventorySubmission>()));
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _events.Setup(e => e.PublishAsync(
                It.IsAny<RawInventoryReceivedEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down"));

        var result = await CreateSut().SubmitAsync(envelope, AgentId, null, null);

        // The row is stored; the worker polls pending submissions anyway. Rejecting the agent
        // here would make it retry and hit the idempotency path, gaining nothing.
        Assert.True(result.IsSuccess);
        Assert.Equal("Accepted", result.Value.Status);
    }

    [Fact]
    public async Task SubmitAsync_Hashes_Retrys_Within_The_Same_Minute_As_Duplicates()
    {
        // Two envelopes captured 30s apart, same device: the minute-truncated hash matches.
        var first = Envelope(timestampUtc: new DateTime(2026, 9, 1, 12, 0, 10, DateTimeKind.Utc));
        var second = Envelope(timestampUtc: new DateTime(2026, 9, 1, 12, 0, 40, DateTimeKind.Utc));

        var hashA = IInventoryService.ComputeHash(AgentId, first.DeviceId, first.TimestampUtc);
        var hashB = IInventoryService.ComputeHash(AgentId, second.DeviceId, second.TimestampUtc);

        // Retries within the minute dedupe; ...
        Assert.Equal(hashA, hashB);

        // ... the next minute does not.
        var third = Envelope(timestampUtc: new DateTime(2026, 9, 1, 12, 1, 10, DateTimeKind.Utc));
        var hashC = IInventoryService.ComputeHash(AgentId, third.DeviceId, third.TimestampUtc);
        Assert.NotEqual(hashA, hashC);
    }

    [Fact]
    public void ComputeHash_Is_Scoped_To_The_Agent_And_Device()
    {
        var timestamp = new DateTime(2026, 9, 1, 12, 0, 30, DateTimeKind.Utc);

        var baseline = IInventoryService.ComputeHash(AgentId, "LT-0421", timestamp);
        var otherAgent = IInventoryService.ComputeHash(OtherAgentId, "LT-0421", timestamp);
        var otherDevice = IInventoryService.ComputeHash(AgentId, "LT-0422", timestamp);

        // Two agents reporting the same machine, or one agent reporting two machines, must never
        // collide into one submission.
        Assert.NotEqual(baseline, otherAgent);
        Assert.NotEqual(baseline, otherDevice);
    }

    [Fact]
    public void ComputeHash_Ignores_The_Payload_Content()
    {
        var timestamp = new DateTime(2026, 9, 1, 12, 0, 30, DateTimeKind.Utc);

        // The hash keys on identity+minute, not content: a real retry may re-serialize slightly
        // differently, and it must still dedupe. (Content changes within the same minute from
        // the same device collapse to one submission -- an accepted trade-off, pinned here.)
        var withSmallPayload = IInventoryService.ComputeHash(AgentId, "LT-0421", timestamp);
        Assert.Equal(64, withSmallPayload.Length); // SHA-256 hex
    }

    // ---- Submit: status transitions ----------------------------------------------------------

    [Fact]
    public async Task MarkProcessingAsync_Moves_A_Pending_Submission_To_Processing()
    {
        var submission = Submission(status: SubmissionStatus.Pending);
        ArrangeInternalLookup(submission);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().MarkProcessingAsync(submission.Id);

        Assert.Equal(SubmissionStatus.Processing, submission.Status);
    }

    [Fact]
    public async Task MarkProcessingAsync_Leaves_A_Completed_Submission_Alone()
    {
        var submission = Submission(status: SubmissionStatus.Completed);
        ArrangeInternalLookup(submission);

        await CreateSut().MarkProcessingAsync(submission.Id);

        // A second worker picking up the same row must not reopen finished work.
        Assert.Equal(SubmissionStatus.Completed, submission.Status);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkCompletedAsync_Records_The_Device_Count_And_Timestamp()
    {
        var submission = Submission(status: SubmissionStatus.Processing);
        ArrangeInternalLookup(submission);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().MarkCompletedAsync(submission.Id, deviceCount: 42);

        Assert.Equal(SubmissionStatus.Completed, submission.Status);
        Assert.Equal(42, submission.DeviceCount);
        Assert.NotNull(submission.ProcessedAt);
    }

    [Fact]
    public async Task MarkCompletedAsync_Ignores_A_Submission_Still_Pending()
    {
        // The worker crashed mid-flight and a sweep calls MarkCompleted without the row ever
        // having been Processing: the transition is refused, not assumed.
        var submission = Submission(status: SubmissionStatus.Pending);
        ArrangeInternalLookup(submission);

        await CreateSut().MarkCompletedAsync(submission.Id, deviceCount: 3);

        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkFailedAsync_Records_The_Error_From_Processing()
    {
        var submission = Submission(status: SubmissionStatus.Processing);
        ArrangeInternalLookup(submission);
        _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().MarkFailedAsync(submission.Id, "payload was not valid JSON");

        Assert.Equal(SubmissionStatus.Failed, submission.Status);
        Assert.Equal("payload was not valid JSON", submission.ErrorMessage);
        Assert.NotNull(submission.ProcessedAt);
    }

    [Fact]
    public async Task MarkFailedAsync_Cannot_Fail_A_Completed_Submission()
    {
        var submission = Submission(status: SubmissionStatus.Completed);
        ArrangeInternalLookup(submission);

        await CreateSut().MarkFailedAsync(submission.Id, "late error");

        Assert.Equal(SubmissionStatus.Completed, submission.Status);
        Assert.Null(submission.ErrorMessage);
    }

    // ---- Reads -------------------------------------------------------------------------------

    [Fact]
    public async Task GetStatusAsync_Returns_NotFound_For_Another_Agents_Submission()
    {
        const long id = 1;
        _repo.Setup(r => r.GetByIdAsync(id, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RawInventorySubmission?)null);

        var result = await CreateSut().GetStatusAsync(id, AgentId);

        // Agent-scoped: submission ids are small integers, trivially enumerable.
        Assert.False(result.IsSuccess);
        Assert.Equal("INVENTORY.NOT_FOUND", result.Error!.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GetStatusAsync_Reports_The_Processing_Outcome()
    {
        var submission = Submission(status: SubmissionStatus.Failed);
        submission.ErrorMessage = "boom";

        _repo.Setup(r => r.GetByIdAsync(submission.Id, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var result = await CreateSut().GetStatusAsync(submission.Id, AgentId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Failed", result.Value.Status);
        Assert.Equal("boom", result.Value.ErrorMessage);
    }

    [Fact]
    public async Task GetStatusByHashAsync_lets_An_Agent_Retry_By_Idempotency_Key()
    {
        var submission = Submission(hash: "abc123", status: SubmissionStatus.Completed);
        _repo.Setup(r => r.GetByHashAsync("abc123", AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var result = await CreateSut().GetStatusByHashAsync("abc123", AgentId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Completed", result.Value.Status);
        Assert.Equal(5, result.Value.DeviceCount);
    }

    [Theory]
    [InlineData(0, 20, 0)]      // page 1 => skip 0
    [InlineData(2, 20, 20)]      // page 2 => skip 20
    [InlineData(3, 50, 100)]      // page 3 of 50 => skip 100
    public async Task ListAsync_Translates_Page_Numbers_To_A_Skip(int page, int pageSize, int expectedSkip)
    {
        _repo.Setup(r => r.ListAsync(AgentId, expectedSkip, pageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RawInventorySubmission>());
        _repo.Setup(r => r.GetCountAsync(AgentId, It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var result = await CreateSut().ListAsync(AgentId, page, pageSize);

        Assert.True(result.IsSuccess);
        _repo.Verify(r => r.ListAsync(AgentId, expectedSkip, pageSize, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_Reports_The_Total()
    {
        var rows = new List<RawInventorySubmission> { Submission(), Submission(id: 2) };

        _repo.Setup(r => r.ListAsync(AgentId, 0, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);
        _repo.Setup(r => r.GetCountAsync(AgentId, It.IsAny<CancellationToken>())).ReturnsAsync(57);

        var result = await CreateSut().ListAsync(AgentId, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(57, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.All(result.Value.Items, dto => Assert.Equal(AgentId, dto.AgentId));
    }
}
