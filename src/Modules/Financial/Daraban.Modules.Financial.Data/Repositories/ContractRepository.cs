using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Financial.Data.Repositories;

public class ContractRepository : IContractRepository
{
    private readonly FinancialDbContext _context;

    public ContractRepository(FinancialDbContext context)
    {
        _context = context;
    }

    public async Task<Contract?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Contracts.FindAsync(new object[] { id }, ct);
    }

    public async Task<Contract?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Contracts
            .Include(c => c.ContractType)
            .Include(c => c.Supplier)
            .Include(c => c.ContractAssets)
            .Include(c => c.ContractCosts)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Contract> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        ContractStatus? status,
        Guid? supplierId,
        Guid? contractTypeId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Contracts
            .Where(c => c.EntityId == entityNodeId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search));

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (supplierId.HasValue)
            query = query.Where(c => c.SupplierId == supplierId.Value);

        if (contractTypeId.HasValue)
            query = query.Where(c => c.ContractTypeId == contractTypeId.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Contract contract, CancellationToken ct = default)
    {
        await _context.Contracts.AddAsync(contract, ct);
    }

    public async Task UpdateAsync(Contract contract, CancellationToken ct = default)
    {
        _context.Contracts.Update(contract);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Contracts.AnyAsync(c => c.Id == id, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
