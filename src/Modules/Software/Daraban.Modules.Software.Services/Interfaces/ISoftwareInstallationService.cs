using Daraban.Modules.Software.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Services.Interfaces;

public interface ISoftwareInstallationService
{
    Task<Result<SoftwareInstallationPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        Guid? softwareId,
        Guid? licenseId,
        Guid? assetId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<SoftwareInstallationDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SoftwareInstallationDto>>> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SoftwareInstallationDto>>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default);
    Task<Result<SoftwareInstallationDto>> CreateAsync(CreateSoftwareInstallationRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> UninstallAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<AssetSoftwareSummaryDto>> GetAssetSummaryAsync(Guid assetId, CancellationToken ct = default);
}
