using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Services.Interfaces;

public interface ISoftwareService
{
    Task<Result<SoftwarePagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        SoftwareCategory? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<SoftwareDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<SoftwareDto>> CreateAsync(CreateSoftwareRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<SoftwareDto>> UpdateAsync(Guid id, UpdateSoftwareRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
}
