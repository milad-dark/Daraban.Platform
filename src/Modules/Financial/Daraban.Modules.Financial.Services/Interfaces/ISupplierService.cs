using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services.Interfaces;

public interface ISupplierService
{
    Task<Result<SupplierPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        SupplierType? type,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<SupplierDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<SupplierDto>> CreateAsync(CreateSupplierRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<SupplierDto>> UpdateAsync(Guid id, UpdateSupplierRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
}
