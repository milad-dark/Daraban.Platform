using Daraban.Modules.Software.Data.Entities;

namespace Daraban.Modules.Software.Data.Repositories;

public interface ISoftwareInstallationRepository
{
    Task<SoftwareInstallation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<SoftwareInstallation> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        Guid? softwareId,
        Guid? licenseId,
        Guid? assetId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task<IReadOnlyList<SoftwareInstallation>> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default);
    Task<IReadOnlyList<SoftwareInstallation>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default);
    Task<int> GetActiveCountByLicenseIdAsync(Guid licenseId, CancellationToken ct = default);
    Task AddAsync(SoftwareInstallation installation, CancellationToken ct = default);
    Task UpdateAsync(SoftwareInstallation installation, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> AssetHasInstallationAsync(Guid assetId, Guid softwareId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
