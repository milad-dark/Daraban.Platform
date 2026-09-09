using Daraban.Modules.Reporting.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Reporting.Data.Repositories;

/// <summary>EF Core persistence for report definitions (Task 7.2).</summary>
public class ReportDefinitionRepository(ReportingDbContext db) : IReportDefinitionRepository
{
    public Task<ReportDefinition?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Definitions.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, ct);

    public async Task<IReadOnlyList<ReportDefinition>> ListAsync(
        Guid entityId, bool scheduledOnly = false, CancellationToken ct = default)
    {
        var query = db.Definitions.AsNoTracking()
            .Where(d => d.EntityId == entityId && !d.IsDeleted);

        if (scheduledOnly)
            query = query.Where(d => d.ScheduleEnabled && d.Schedule != null);

        return await query.OrderBy(d => d.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReportDefinition>> ListScheduledAsync(CancellationToken ct = default)
    {
        return await db.Definitions.AsNoTracking()
            .Where(d => d.ScheduleEnabled && d.Schedule != null && !d.IsDeleted)
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
    }

    public Task<bool> NameExistsAsync(Guid entityId, string name, CancellationToken ct = default) =>
        db.Definitions.AsNoTracking()
            .AnyAsync(d => d.EntityId == entityId && d.Name == name && !d.IsDeleted, ct);

    public async Task AddAsync(ReportDefinition definition, CancellationToken ct = default) =>
        await db.Definitions.AddAsync(definition, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

/// <summary>EF Core persistence for generated report records (Task 7.2). The worker uses a
/// tracked GetAsync (update path); the API's read paths use AsNoTracking.</summary>
public class SavedReportRepository(ReportingDbContext db) : ISavedReportRepository
{
    public Task<SavedReport?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.SavedReports.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<SavedReport>> ListByDefinitionAsync(
        Guid definitionId, int limit = 20, CancellationToken ct = default)
    {
        return await db.SavedReports.AsNoTracking()
            .Where(r => r.DefinitionId == definitionId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task AddAsync(SavedReport report, CancellationToken ct = default) =>
        await db.SavedReports.AddAsync(report, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
