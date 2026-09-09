using Daraban.Modules.Dashboard.Data.Entities;

namespace Daraban.Modules.Dashboard.Data.Repositories;

/// <summary>Persistence contract for per-user dashboard layouts.</summary>
public interface IDashboardLayoutRepository
{
    Task<DashboardLayout?> GetAsync(Guid userId, string name, CancellationToken ct = default);
    Task UpsertAsync(DashboardLayout layout, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
