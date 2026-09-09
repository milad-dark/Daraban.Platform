using Daraban.Modules.Reporting.Data.Entities;

namespace Daraban.Modules.Reporting.Data.Repositories;

/// <summary>Persistence contract for report definitions (Task 7.2).</summary>
public interface IReportDefinitionRepository
{
    Task<ReportDefinition?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReportDefinition>> ListAsync(Guid entityId, bool scheduledOnly = false, CancellationToken ct = default);
    /// <summary>All schedule-enabled definitions across every entity scope -- the scheduled
    /// report cron sweeps these (Task 7.2).</summary>
    Task<IReadOnlyList<ReportDefinition>> ListScheduledAsync(CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid entityId, string name, CancellationToken ct = default);
    Task AddAsync(ReportDefinition definition, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Persistence contract for generated report records (Task 7.2).</summary>
public interface ISavedReportRepository
{
    Task<SavedReport?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SavedReport>> ListByDefinitionAsync(Guid definitionId, int limit = 20, CancellationToken ct = default);
    Task AddAsync(SavedReport report, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
