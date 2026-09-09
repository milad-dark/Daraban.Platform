using Daraban.Modules.Reporting.Services.Reports;

namespace Daraban.Modules.Reporting.Services.Dtos;

// ---- Catalog -------------------------------------------------------------------

/// <summary>One reportable dataset as exposed by GET /api/v1/reports/definitions.</summary>
public sealed record ReportDatasetDto(
    string Key,
    string Name,
    string Description,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<string> SupportedFilters);

public sealed record ReportColumnDto(string Key, string Header, string Kind);

// ---- Definitions ----------------------------------------------------------------

/// <summary>A saved report definition as exposed over the API.</summary>
public sealed record ReportDefinitionDto(
    Guid Id,
    string Name,
    string Module,
    IReadOnlyDictionary<string, string> Filters,
    IReadOnlyList<string> Columns,
    string Format,
    string? Schedule,
    bool ScheduleEnabled,
    DateTimeOffset UpdatedAt);

/// <summary>Request body for POST /api/v1/reports/definitions.</summary>
public sealed record CreateReportDefinitionRequest(
    string Name,
    string Module,
    IReadOnlyDictionary<string, string>? Filters,
    IReadOnlyList<string>? Columns,
    string Format,
    string? Schedule,
    bool ScheduleEnabled);

// ---- Generation / results ---------------------------------------------------------

/// <summary>Result of POST /api/v1/reports/{id}/generate -- the queued run, not the artifact.</summary>
public sealed record ReportGenerationDto(
    Guid SavedReportId,
    Guid DefinitionId,
    string Status,
    string Trigger,
    DateTimeOffset CreatedAt);

/// <summary>A generated report row as exposed over the API.</summary>
public sealed record SavedReportDto(
    Guid Id,
    Guid DefinitionId,
    string Status,
    string Trigger,
    string Format,
    long? RowCount,
    long? FileSizeBytes,
    string? FailureReason,
    DateTimeOffset? GeneratedAt,
    DateTimeOffset CreatedAt);
