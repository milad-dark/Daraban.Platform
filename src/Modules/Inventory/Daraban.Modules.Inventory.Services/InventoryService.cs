using Daraban.Modules.Inventory.Data.Entities;
using Daraban.Modules.Inventory.Data.Repositories;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Contracts.Inventory;
using System.Text.Json;

namespace Daraban.Modules.Inventory.Services;

/// <summary>
/// Implements the inventory submission pipeline (Task 4.3 SS3.1):
/// 1. Compute idempotency hash → reject duplicates
/// 2. Store raw envelope in raw_inventory_submissions
/// 3. Publish RawInventoryReceivedEvent → background worker extracts structured data
/// </summary>
public class InventoryService(IInventoryRepository repo, IEventPublisher eventPublisher) : IInventoryService
{
    public async Task<Result<SubmissionAcceptedResponse>> SubmitAsync(
        AgentEnvelope envelope,
        Guid agentId,
        Guid? entityId,
        string? ipAddress,
        CancellationToken ct = default)
    {
        // Compute idempotency hash
        var hash = IInventoryService.ComputeHash(agentId, envelope.DeviceId, envelope.TimestampUtc);

        // Check for duplicate submission (same agent + device + minute)
        var existing = await repo.GetByHashAsync(hash, agentId, ct);
        if (existing is not null)
        {
            // Idempotent replay: return the original submission's status
            return Result.Success(new SubmissionAcceptedResponse(
                existing.Id,
                "Duplicate",
                existing.ReceivedAt));
        }

        // Serialize the full envelope and the raw content
        var fullEnvelope = JsonSerializer.Serialize(envelope);
        var rawPayload = envelope.Content is not null
            ? JsonSerializer.Serialize(envelope.Content)
            : "{}";

        var submission = new RawInventorySubmission
        {
            SubmissionHash = hash,
            AgentId = agentId,
            DeviceId = envelope.DeviceId,
            ItemType = envelope.ItemType,
            Action = envelope.Action,
            RawPayload = rawPayload,
            FullEnvelope = fullEnvelope,
            Status = SubmissionStatus.Pending,
            EntityId = entityId,
            SubmittedAt = envelope.TimestampUtc,
            ReceivedAt = DateTimeOffset.UtcNow,
            IpAddress = ipAddress,
        };

        repo.Add(submission);
        await repo.SaveChangesAsync(ct);

        // Publish event for background processing (fire-and-forget — processing failure
        // does not prevent the agent from receiving a 202 Accepted)
        try
        {
            var submissionGuid = new Guid(submission.Id.ToString("D").PadLeft(32, '0'));
            await eventPublisher.PublishAsync(new RawInventoryReceivedEvent(
                submissionGuid, agentId, IsPartial: false), ct);
        }
        catch
        {
            // Event publishing failure is non-fatal — the background worker will pick up
            // pending submissions via polling even if the event is lost.
        }

        return Result.Success(new SubmissionAcceptedResponse(
            submission.Id,
            "Accepted",
            submission.ReceivedAt));
    }

    public async Task<Result<SubmissionStatusResponse>> GetStatusAsync(
        long submissionId, Guid agentId, CancellationToken ct = default)
    {
        var submission = await repo.GetByIdAsync(submissionId, agentId, ct);
        if (submission is null)
            return Result.Failure<SubmissionStatusResponse>(
                new Error("INVENTORY.NOT_FOUND", "Submission not found.", ErrorType.NotFound));

        return Result.Success(MapToStatus(submission));
    }

    public async Task<Result<SubmissionStatusResponse>> GetStatusByHashAsync(
        string submissionHash, Guid agentId, CancellationToken ct = default)
    {
        var submission = await repo.GetByHashAsync(submissionHash, agentId, ct);
        if (submission is null)
            return Result.Failure<SubmissionStatusResponse>(
                new Error("INVENTORY.NOT_FOUND", "Submission not found.", ErrorType.NotFound));

        return Result.Success(MapToStatus(submission));
    }

    public async Task<Result<SubmissionPagedResult>> ListAsync(
        Guid agentId, int page, int pageSize, CancellationToken ct = default)
    {
        var skip = (page - 1) * pageSize;
        var items = await repo.ListAsync(agentId, skip, pageSize, ct);
        var total = await repo.GetCountAsync(agentId, ct);

        return Result.Success(new SubmissionPagedResult(
            items.Select(MapToDto).ToList(),
            total, page, pageSize));
    }

    public async Task MarkProcessingAsync(long submissionId, CancellationToken ct = default)
    {
        // No agentId scoping here — the background worker doesn't have an agent context
        // (it's processing whatever's in the queue). This is intentional: the worker is
        // a trusted internal component, not an external API caller.
        var submission = await FindByIdInternalAsync(submissionId, ct);
        if (submission is not null && submission.Status == SubmissionStatus.Pending)
        {
            submission.Status = SubmissionStatus.Processing;
            await repo.SaveChangesAsync(ct);
        }
    }

    public async Task MarkCompletedAsync(long submissionId, int deviceCount, CancellationToken ct = default)
    {
        var submission = await FindByIdInternalAsync(submissionId, ct);
        if (submission is not null && submission.Status == SubmissionStatus.Processing)
        {
            submission.Status = SubmissionStatus.Completed;
            submission.DeviceCount = deviceCount;
            submission.ProcessedAt = DateTimeOffset.UtcNow;
            await repo.SaveChangesAsync(ct);
        }
    }

    public async Task MarkFailedAsync(long submissionId, string errorMessage, CancellationToken ct = default)
    {
        var submission = await FindByIdInternalAsync(submissionId, ct);
        if (submission is not null && submission.Status == SubmissionStatus.Processing)
        {
            submission.Status = SubmissionStatus.Failed;
            submission.ErrorMessage = errorMessage;
            submission.ProcessedAt = DateTimeOffset.UtcNow;
            await repo.SaveChangesAsync(ct);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<RawInventorySubmission?> FindByIdInternalAsync(long id, CancellationToken ct)
        => await repo.GetByIdAsync(id, Guid.Empty, ct)
           ?? await repo.GetByIdUnscopedAsync(id, ct);

    private static SubmissionStatusResponse MapToStatus(RawInventorySubmission s) => new(
        s.Id,
        s.Status.ToString(),
        s.DeviceCount,
        s.ReceivedAt,
        s.ProcessedAt,
        s.ErrorMessage);

    private static SubmissionDto MapToDto(RawInventorySubmission s) => new(
        s.Id,
        s.AgentId,
        s.DeviceId,
        s.ItemType,
        s.Action,
        s.Status.ToString(),
        s.DeviceCount,
        s.SubmittedAt,
        s.ReceivedAt,
        s.ProcessedAt,
        s.ErrorMessage);
}
