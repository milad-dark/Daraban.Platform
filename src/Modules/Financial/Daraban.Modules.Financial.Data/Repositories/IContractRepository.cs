using Daraban.Modules.Financial.Data.Entities;

namespace Daraban.Modules.Financial.Data.Repositories;

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Contract?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<Contract> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        ContractStatus? status,
        Guid? supplierId,
        Guid? contractTypeId,
        int page,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Contract contract, CancellationToken ct = default);
    Task UpdateAsync(Contract contract, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
