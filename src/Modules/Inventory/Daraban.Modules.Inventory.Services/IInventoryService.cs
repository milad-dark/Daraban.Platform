using Daraban.Platform.Common;

namespace Daraban.Modules.Inventory.Services;

/// <summary>
/// Manages raw inventory submissions from agents (Task 4.3).
/// The controller handles envelope ingestion and auth; this service handles
/// idempotency, storage, and background processing dispatch.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Accept a raw inventory envelope from an agent. Performs idempotency check,
    /// stores the raw payload, and publishes an event for background processing.
    /// Returns 202 Accepted with a submission ID.
    /// </summary>
    Task<Result<SubmissionAcceptedResponse>> SubmitAsync(
        AgentEnvelope envelope,
        Guid agentId,
        Guid? entityId,
        string? ipAddress,
        CancellationToken ct = default);

    /// <summary>
    /// Get the status of a specific submission (agent can poll for processing result).
    /// </summary>
    Task<Result<SubmissionStatusResponse>> GetStatusAsync(
        long submissionId,
        Guid agentId,
        CancellationToken ct = default);

    /// <summary>
    /// Get submission status by idempotency hash (agent retry → same result).
    /// </summary>
    Task<Result<SubmissionStatusResponse>> GetStatusByHashAsync(
        string submissionHash,
        Guid agentId,
        CancellationToken ct = default);

    /// <summary>
    /// List submissions for an agent (paginated).
    /// </summary>
    Task<Result<SubmissionPagedResult>> ListAsync(
        Guid agentId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Mark a submission as Processing (called by background worker when picked up).
    /// </summary>
    Task MarkProcessingAsync(long submissionId, CancellationToken ct = default);

    /// <summary>
    /// Mark a submission as Completed with the number of devices extracted.
    /// </summary>
    Task MarkCompletedAsync(long submissionId, int deviceCount, CancellationToken ct = default);

    /// <summary>
    /// Mark a submission as Failed with the error message.
    /// </summary>
    Task MarkFailedAsync(long submissionId, string errorMessage, CancellationToken ct = default);

    /// <summary>
    /// Generate the idempotency hash for a submission envelope.
    /// </summary>
    static string ComputeHash(Guid agentId, string deviceId, DateTime timestampUtc)
    {
        // Truncate timestamp to the minute to allow retries within the same minute
        var minute = new DateTime(timestampUtc.Year, timestampUtc.Month, timestampUtc.Day,
            timestampUtc.Hour, timestampUtc.Minute, 0, DateTimeKind.Utc);
        var input = $"{agentId}:{deviceId}:{minute:O}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
