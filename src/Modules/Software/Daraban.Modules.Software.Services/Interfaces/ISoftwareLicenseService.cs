using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Services.Interfaces;

public interface ISoftwareLicenseService
{
    Task<Result<SoftwareLicensePagedResult>> GetPagedAsync(
        Guid entityNodeId,
        Guid? softwareId,
        LicenseType? type,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<SoftwareLicenseDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<SoftwareLicenseDto>>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default);
    Task<Result<SoftwareLicenseDto>> CreateAsync(CreateSoftwareLicenseRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<SoftwareLicenseDto>> UpdateAsync(Guid id, UpdateSoftwareLicenseRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<LicenseComplianceResult>> CheckComplianceAsync(Guid id, CancellationToken ct = default);
}
