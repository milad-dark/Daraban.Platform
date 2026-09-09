using Daraban.Modules.Reporting.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Reporting.Services.Interfaces;

/// <summary>Reporting feature contract (Task 7.2). All expected failures return
/// <see cref="Result{T}"/>; the controller maps Error to ProblemDetails in one place.</summary>
public interface IReportingService
{
    /// <summary>The closed catalog of reportable datasets (GET /api/v1/reports/definitions).</summary>
    Result<IReadOnlyList<ReportDatasetDto>> GetCatalog();

    /// <summary>Saved definitions for the caller's entity scope.</summary>
    Task<Result<IReadOnlyList<ReportDefinitionDto>>> ListDefinitionsAsync(Guid entityId, CancellationToken ct = default);

    /// <summary>Creates and persists a definition (POST /api/v1/reports/definitions).</summary>
    Task<Result<ReportDefinitionDto>> CreateDefinitionAsync(Guid entityId, Guid userId, CreateReportDefinitionRequest request, CancellationToken ct = default);

    /// <summary>Queues a generation run: inserts a Pending SavedReport and publishes
    /// ReportRequestedEvent for Worker.Reporting (POST /api/v1/reports/{id}/generate).</summary>
    Task<Result<ReportGenerationDto>> GenerateAsync(Guid definitionId, Guid entityId, Guid userId, CancellationToken ct = default);

    /// <summary>Generation history for one definition.</summary>
    Task<Result<IReadOnlyList<SavedReportDto>>> ListRunsAsync(Guid definitionId, Guid entityId, CancellationToken ct = default);

    /// <summary>Streams a completed artifact. Enforces entity scope + completed status
    /// (GET /api/v1/reports/{id}/download).</summary>
    Task<Result<(Stream Stream, string ContentType, string FileName)>> DownloadAsync(Guid savedReportId, Guid entityId, CancellationToken ct = default);
}
