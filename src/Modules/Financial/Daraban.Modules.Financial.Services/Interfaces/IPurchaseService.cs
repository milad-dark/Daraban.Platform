using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services.Interfaces;

public interface IPurchaseService
{
    Task<Result<PurchasePagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        PurchaseStatus? status,
        Guid? supplierId,
        Guid? budgetId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<PurchaseDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<PurchaseDto>> CreateAsync(CreatePurchaseRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<PurchaseDto>> UpdateAsync(Guid id, UpdatePurchaseRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<PurchaseDto>> ChangeStatusAsync(Guid id, PurchaseStatus newStatus, Guid actorUserId, CancellationToken ct = default);
    Task<Result<PurchaseDto>> AddItemAsync(Guid purchaseId, CreatePurchaseItemRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<PurchaseDto>> RemoveItemAsync(Guid purchaseId, Guid itemId, Guid actorUserId, CancellationToken ct = default);
}
