using Daraban.Platform.Common;

namespace Daraban.Modules.Reporting.Data.Entities;

/// <summary>Where a generated report file physically lives.</summary>
public enum SavedReportStorageKind
{
    /// <summary>Local/attached filesystem directory (ReportStore:RootPath).</summary>
    FileSystem = 1,
}

/// <summary>
/// One generated output of a <see cref="ReportDefinition"/> (Task 7.2). The worker writes the
/// rendered file to the configured file store and records the reference here; download
/// endpoints stream from the store through this row, never reconstruct from the DB.
/// </summary>
public class SavedReport : BaseEntity
{
    public Guid DefinitionId { get; set; }

    /// <summary>Entity scope the data was queried under -- downloads re-check against the
    /// caller's scope so one entity can never read another's reports.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Who triggered this generation (user id for manual runs, definition owner for scheduled).</summary>
    public Guid? RequestedByUserId { get; set; }

    /// <summary>What triggered the run: "manual" or "schedule".</summary>
    public string Trigger { get; set; } = ReportTriggers.Manual;

    public string Format { get; set; } = "csv";

    /// <summary>Pending | Completed | Failed.</summary>
    public SavedReportStatus Status { get; set; } = SavedReportStatus.Pending;

    /// <summary>The generated artifact's reference in the file store -- a relative path under
    /// the store root. Never an absolute path or URL: the store resolves it.</summary>
    public string? StorageReference { get; set; }

    public SavedReportStorageKind StorageKind { get; set; } = SavedReportStorageKind.FileSystem;

    /// <summary>Row counts / row source snapshot at generation time.</summary>
    public long? RowCount { get; set; }

    public long? FileSizeBytes { get; set; }

    /// <summary>Renderer failure message when Status = Failed.</summary>
    public string? FailureReason { get; set; }

    public DateTimeOffset? GeneratedAt { get; set; }
}

public enum SavedReportStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
}

/// <summary>Well-known values for <see cref="SavedReport.Trigger"/>.</summary>
public static class ReportTriggers
{
    public const string Manual = "manual";
    public const string Schedule = "schedule";
}
