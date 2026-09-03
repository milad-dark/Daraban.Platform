using Daraban.Modules.Financial.Data.Entities;

namespace Daraban.Modules.Financial.Data.Repositories;

public interface IPurchaseRepository
{
    Task<Purchase?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Purchase?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Purchase> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        PurchaseStatus? status,
        Guid? supplierId,
        Guid? budgetId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<Purchase?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task AddAsync(Purchase purchase, CancellationToken ct = default);
    Task UpdateAsync(Purchase purchase, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
