using Daraban.Modules.Software.Data.Entities;

namespace Daraban.Modules.Software.Data.Repositories;

public interface ISoftwareLicenseRepository
{
    Task<SoftwareLicense?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SoftwareLicense?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<SoftwareLicense> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        Guid? softwareId,
        LicenseType? type,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<IReadOnlyList<SoftwareLicense>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default);
    Task AddAsync(SoftwareLicense license, CancellationToken ct = default);
    Task UpdateAsync(SoftwareLicense license, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
