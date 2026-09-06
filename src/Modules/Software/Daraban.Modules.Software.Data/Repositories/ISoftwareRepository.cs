using Daraban.Modules.Software.Data.Entities;

namespace Daraban.Modules.Software.Data.Repositories;

public interface ISoftwareRepository
{
    Task<SoftwareProduct?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SoftwareProduct?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<SoftwareProduct> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        SoftwareCategory? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(SoftwareProduct software, CancellationToken ct = default);
    Task UpdateAsync(SoftwareProduct software, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId = null, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
