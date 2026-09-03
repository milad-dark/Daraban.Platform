using Daraban.Modules.Financial.Data.Entities;

namespace Daraban.Modules.Financial.Data.Repositories;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Supplier?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        SupplierType? type,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Supplier supplier, CancellationToken ct = default);
    Task UpdateAsync(Supplier supplier, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId = null, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
