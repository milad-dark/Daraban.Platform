using Daraban.Modules.Reporting.Data.Entities;
using Daraban.Modules.Reporting.Data.Repositories;
using Daraban.Modules.Reporting.Services.Reports;
using Daraban.Modules.Reporting.Services.Rendering;
using Daraban.Modules.Reporting.Services.Storage;
using Daraban.Platform.Contracts.Reporting;
using Daraban.Platform.Messaging;
using Microsoft.Extensions.Options;

namespace Daraban.Workers.Reporting;

/// <summary>
/// Consumes ReportRequestedEvent (Task 7.2): loads the definition, pulls the dataset through
/// the matching IReportDataProvider, renders via the format's IReportRenderer, writes the
/// artifact to the file store, and flips the SavedReport row to Completed or Failed.
///
/// The message loop acks only on success -- a crash mid-render leaves the row Pending and the
/// message unacked for redelivery, so a generation is never silently lost. FailureReason is
/// persisted for the failed row so the UI can show why a report never appeared.
/// </summary>
public class ReportGenerationConsumer : RabbitMqConsumerBackgroundService<ReportRequestedEvent>
{
    protected override string QueueName => "reporting.generator";
    protected override string RoutingKey => nameof(ReportRequestedEvent);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReportGenerationConsumer> _logger;

    public ReportGenerationConsumer(
        RabbitMqConnectionProvider connectionProvider,
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<ReportGenerationConsumer> logger)
        : base(connectionProvider, options, logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task HandleAsync(ReportRequestedEvent message, CancellationToken ct)
    {
        // The consumer is a long-lived singleton; module services (DbContext, repositories,
        // providers) are scoped -- resolve them per message from a fresh scope.
        using var scope = _scopeFactory.CreateScope();
        var definitions = scope.ServiceProvider.GetRequiredService<IReportDefinitionRepository>();
        var savedReports = scope.ServiceProvider.GetRequiredService<ISavedReportRepository>();
        var providers = scope.ServiceProvider.GetRequiredService<IEnumerable<IReportDataProvider>>();
        var renderers = scope.ServiceProvider.GetRequiredService<IEnumerable<IReportRenderer>>();
        var fileStore = scope.ServiceProvider.GetRequiredService<IReportFileStore>();

        var run = await savedReports.GetAsync(message.SavedReportId, ct)
            ?? throw new InvalidOperationException($"SavedReport {message.SavedReportId} not found -- cannot generate.");

        // Redelivery safety: a Completed run is never rendered twice.
        if (run.Status == SavedReportStatus.Completed)
        {
            _logger.LogInformation("SavedReport {SavedReportId} already completed -- skipping redelivery", run.Id);
            return;
        }

        try
        {
            var definition = await definitions.GetAsync(message.DefinitionId, ct)
                ?? throw new InvalidOperationException($"ReportDefinition {message.DefinitionId} not found.");

            var provider = providers.FirstOrDefault(p =>
                    p.DatasetKey.Equals(definition.Module, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"No IReportDataProvider registered for dataset '{definition.Module}'.");

            var renderer = renderers.FirstOrDefault(r =>
                    r.Format.Equals(definition.Format, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"No IReportRenderer registered for format '{definition.Format}'.");

            var tableResult = await provider.GetTableAsync(
                definition.EntityId, definition.ParseFilters(), definition.ParseColumns(), ct);
            if (!tableResult.IsSuccess)
                throw new InvalidOperationException($"Dataset query failed: {tableResult.Error!.Code} {tableResult.Error.Message}");

            var table = tableResult.Value;
            var content = renderer.Render(table);

            var reference = await fileStore.WriteAsync(run.Id, definition.Format, content, ct);

            run.Status = SavedReportStatus.Completed;
            run.StorageReference = reference;
            run.StorageKind = SavedReportStorageKind.FileSystem;
            run.RowCount = table.Rows.Count;
            run.FileSizeBytes = content.LongLength;
            run.GeneratedAt = DateTimeOffset.UtcNow;
            run.UpdatedAt = DateTimeOffset.UtcNow;
            await savedReports.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Report {SavedReportId} generated: {Dataset} -> {Format}, {Rows} rows, {Bytes} bytes",
                run.Id, definition.Module, definition.Format, table.Rows.Count, content.LongLength);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host shutdown -- not a report failure. Leave the row Pending and the message
            // unacked so the next start picks it up.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report generation failed for SavedReport {SavedReportId}", message.SavedReportId);

            run.Status = SavedReportStatus.Failed;
            run.FailureReason = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            run.GeneratedAt = DateTimeOffset.UtcNow;
            run.UpdatedAt = DateTimeOffset.UtcNow;
            await savedReports.SaveChangesAsync(ct);

            // Swallow: the row records the failure. Re-throwing would nack and redeliver a
            // permanently-broken definition forever.
        }
    }
}
