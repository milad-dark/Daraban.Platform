using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services.Interfaces;

public interface IContractService
{
    Task<Result<ContractPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        ContractStatus? status,
        Guid? supplierId,
        Guid? contractTypeId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Result<ContractDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ContractDto>> CreateAsync(CreateContractRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result<ContractDto>> UpdateAsync(Guid id, UpdateContractRequest request, Guid actorUserId, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default);
    Task<Result<ContractDto>> ChangeStatusAsync(Guid id, ContractStatus newStatus, Guid actorUserId, CancellationToken ct = default);
}
