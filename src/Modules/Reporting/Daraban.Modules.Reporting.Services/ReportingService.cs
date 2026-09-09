using Daraban.Modules.Reporting.Data.Entities;
using Daraban.Modules.Reporting.Data.Repositories;
using Daraban.Modules.Reporting.Services.Dtos;
using Daraban.Modules.Reporting.Services.Interfaces;
using Daraban.Modules.Reporting.Services.Reports;
using Daraban.Modules.Reporting.Services.Rendering;
using Daraban.Modules.Reporting.Services.Storage;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Daraban.Modules.Reporting.Services;

/// <summary>
/// Reporting feature implementation (Task 7.2). Generation is asynchronous by design: the
/// API only inserts a Pending SavedReport and publishes <c>ReportRequestedEvent</c>; the
/// worker renders, writes the file store, and completes the row. Download streams from the
/// file store -- the API process never holds rendered artifacts in memory.
/// </summary>
public sealed class ReportingService(
    IReportDefinitionRepository definitions,
    ISavedReportRepository savedReports,
    IEventPublisher eventPublisher,
    IReportFileStore fileStore,
    IValidator<CreateReportDefinitionRequest> createValidator,
    TimeProvider clock,
    ILogger<ReportingService> logger) : IReportingService
{
    public Result<IReadOnlyList<ReportDatasetDto>> GetCatalog() =>
        Result.Success<IReadOnlyList<ReportDatasetDto>>(ReportCatalog.All.Select(ToDto).ToList());

    public async Task<Result<IReadOnlyList<ReportDefinitionDto>>> ListDefinitionsAsync(
        Guid entityId, CancellationToken ct = default)
    {
        var rows = await definitions.ListAsync(entityId, scheduledOnly: false, ct);
        return Result.Success<IReadOnlyList<ReportDefinitionDto>>(
            rows.Select(ToDto).ToList());
    }

    public async Task<Result<ReportDefinitionDto>> CreateDefinitionAsync(
        Guid entityId, Guid userId, CreateReportDefinitionRequest request, CancellationToken ct = default)
    {
        var validation = await createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<ReportDefinitionDto>(
                new Error("REPORTS.DEFINITION_INVALID", message, ErrorType.Validation));
        }

        if (await definitions.NameExistsAsync(entityId, request.Name, ct))
        {
            return Result.Failure<ReportDefinitionDto>(
                new Error("REPORTS.DEFINITION_DUPLICATE", $"A report named '{request.Name}' already exists.", ErrorType.Conflict));
        }

        var definition = new ReportDefinition
        {
            EntityId = entityId,
            Name = request.Name,
            Module = ReportCatalog.All.First(r => r.Key.Equals(request.Module, StringComparison.OrdinalIgnoreCase)).Key,
            FiltersJson = ReportDefinition.SerializeFilters(request.Filters ?? new Dictionary<string, string>()),
            ColumnsJson = ReportDefinition.SerializeColumns(request.Columns ?? []),
            Format = request.Format.Trim().ToLowerInvariant(),
            Schedule = string.IsNullOrWhiteSpace(request.Schedule) ? null : request.Schedule.Trim(),
            ScheduleEnabled = request.ScheduleEnabled,
            OwnerUserId = userId,
            CreatedById = userId,
            UpdatedById = userId,
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow(),
        };

        try
        {
            await definitions.AddAsync(definition, ct);
            await definitions.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
        {
            // Intentionally narrow: only persistence-layer failures are converted. Unexpected
            // bugs must still crash loudly rather than be swallowed as a "business" error.
            logger.LogError(ex, "Failed to persist report definition '{Name}' for entity {EntityId}", request.Name, entityId);
            return Result.Failure<ReportDefinitionDto>(
                new Error("REPORTS.SAVE_FAILED", "The report definition could not be saved.", ErrorType.BusinessRule));
        }

        return Result.Success(ToDto(definition));
    }

    public async Task<Result<ReportGenerationDto>> GenerateAsync(
        Guid definitionId, Guid entityId, Guid userId, CancellationToken ct = default)
    {
        var definition = await definitions.GetAsync(definitionId, ct);
        if (definition is null || definition.EntityId != entityId)
        {
            return Result.Failure<ReportGenerationDto>(
                new Error("REPORTS.DEFINITION_NOT_FOUND", "Report definition not found.", ErrorType.NotFound));
        }

        var run = new SavedReport
        {
            DefinitionId = definition.Id,
            EntityId = definition.EntityId,
            RequestedByUserId = userId,
            Trigger = ReportTriggers.Manual,
            Format = definition.Format,
            Status = SavedReportStatus.Pending,
            CreatedById = userId,
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow(),
        };

        await savedReports.AddAsync(run, ct);
        await savedReports.SaveChangesAsync(ct);

        await eventPublisher.PublishAsync(new Daraban.Platform.Contracts.Reporting.ReportRequestedEvent(
            run.Id, definition.Id, definition.EntityId, userId, ReportTriggers.Manual), ct);

        logger.LogInformation(
            "Report generation queued: definition {DefinitionId} -> saved report {SavedReportId} (format {Format})",
            definitionId, run.Id, definition.Format);

        return Result.Success(new ReportGenerationDto(
            run.Id, definition.Id, run.Status.ToString(), run.Trigger, run.CreatedAt));
    }

    public async Task<Result<IReadOnlyList<SavedReportDto>>> ListRunsAsync(
        Guid definitionId, Guid entityId, CancellationToken ct = default)
    {
        var definition = await definitions.GetAsync(definitionId, ct);
        if (definition is null || definition.EntityId != entityId)
        {
            return Result.Failure<IReadOnlyList<SavedReportDto>>(
                new Error("REPORTS.DEFINITION_NOT_FOUND", "Report definition not found.", ErrorType.NotFound));
        }

        var runs = await savedReports.ListByDefinitionAsync(definitionId, 20, ct);
        return Result.Success<IReadOnlyList<SavedReportDto>>(runs.Select(ToDto).ToList());
    }

    public async Task<Result<(Stream Stream, string ContentType, string FileName)>> DownloadAsync(
        Guid savedReportId, Guid entityId, CancellationToken ct = default)
    {
        var run = await savedReports.GetAsync(savedReportId, ct);
        if (run is null || run.EntityId != entityId)
        {
            return Result.Failure<(Stream, string, string)>(
                new Error("REPORTS.NOT_FOUND", "Report not found.", ErrorType.NotFound));
        }

        if (run.Status != SavedReportStatus.Completed || run.StorageReference is null)
        {
            return Result.Failure<(Stream, string, string)>(
                new Error("REPORTS.NOT_READY", "This report has not completed generation yet.", ErrorType.BusinessRule));
        }

        var stream = await fileStore.OpenReadAsync(run.StorageReference, ct);
        if (stream is null)
        {
            logger.LogError("Saved report {SavedReportId} references missing artifact '{Reference}'",
                savedReportId, run.StorageReference);
            return Result.Failure<(Stream, string, string)>(
                new Error("REPORTS.ARTIFACT_MISSING", "The report file is no longer available.", ErrorType.BusinessRule));
        }

        var fileName = $"report-{run.DefinitionId:N}-{savedReportId:N}.{ReportFormats.FileExtension(run.Format)}";
        return Result.Success((stream, ReportFormats.ContentType(run.Format), fileName));
    }

    // ---- Mapping ------------------------------------------------------------------

    private static ReportDatasetDto ToDto(ReportCatalog.ReportDefinitionMetadata metadata) =>
        new(
            metadata.Key,
            metadata.Name,
            metadata.Description,
            metadata.Columns.Select(c => new ReportColumnDto(c.Key, c.Header, c.Kind.ToString())).ToList(),
            metadata.SupportedFilters);

    private static ReportDefinitionDto ToDto(ReportDefinition definition) =>
        new(
            definition.Id,
            definition.Name,
            definition.Module,
            definition.ParseFilters(),
            definition.ParseColumns(),
            definition.Format,
            definition.Schedule,
            definition.ScheduleEnabled,
            definition.UpdatedAt);

    private static SavedReportDto ToDto(SavedReport run) =>
        new(
            run.Id,
            run.DefinitionId,
            run.Status.ToString(),
            run.Trigger,
            run.Format,
            run.RowCount,
            run.FileSizeBytes,
            run.FailureReason,
            run.GeneratedAt,
            run.CreatedAt);
}
