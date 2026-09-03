using Daraban.Modules.Financial.Data.Entities;

namespace Daraban.Modules.Financial.Data.Repositories;

public interface IBudgetRepository
{
    Task<Budget?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Budget?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Budget> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<Budget?> GetByNameAsync(string name, Guid entityNodeId, CancellationToken ct = default);
    Task AddAsync(Budget budget, CancellationToken ct = default);
    Task UpdateAsync(Budget budget, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId = null, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
