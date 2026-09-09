namespace Daraban.Platform.Contracts.Reporting;

/// <summary>
/// Cross-module command-style event published by the Reporting API (manual generation) and
/// by the Automation cron (scheduled generation). Consumed exclusively by Worker.Reporting,
/// which renders the artifact, writes it to the file store, and flips the matching SavedReport
/// row to Completed/Failed (Task 7.2).
/// </summary>
public sealed record ReportRequestedEvent(
    Guid SavedReportId,
    Guid DefinitionId,
    Guid EntityId,
    Guid? RequestedByUserId,
    string Trigger);
