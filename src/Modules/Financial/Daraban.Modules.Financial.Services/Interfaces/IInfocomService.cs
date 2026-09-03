using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services.Interfaces;

public interface IInfocomService
{
    Task<Result<InfocomPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        Guid? supplierId,
        Guid? budgetId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<InfocomDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<InfocomDto>> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default);
    Task<Result<InfocomDto>> CreateAsync(CreateInfocomRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<InfocomDto>> UpdateAsync(Guid id, UpdateInfocomRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<DepreciationCalculationResult>> CalculateDepreciationAsync(Guid id, CancellationToken ct = default);
}
