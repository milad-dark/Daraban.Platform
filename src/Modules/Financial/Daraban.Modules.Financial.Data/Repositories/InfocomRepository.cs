using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Financial.Data.Repositories;

public class InfocomRepository : IInfocomRepository
{
    private readonly FinancialDbContext _context;

    public InfocomRepository(FinancialDbContext context)
    {
        _context = context;
    }

    public async Task<Infocom?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Infocoms.FindAsync(new object[] { id }, ct);
    }

    public async Task<Infocom?> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default)
    {
        return await _context.Infocoms
            .Include(i => i.Supplier)
            .Include(i => i.Budget)
            .FirstOrDefaultAsync(i => i.AssetId == assetId && i.IsActive, ct);
    }

    public async Task<(IReadOnlyList<Infocom> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        Guid? supplierId,
        Guid? budgetId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Infocoms
            .Where(i => i.EntityId == entityNodeId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i => (i.InvoiceNumber != null && i.InvoiceNumber.Contains(search)) ||
                                     (i.PurchaseOrderNumber != null && i.PurchaseOrderNumber.Contains(search)));

        if (supplierId.HasValue)
            query = query.Where(i => i.SupplierId == supplierId.Value);

        if (budgetId.HasValue)
            query = query.Where(i => i.BudgetId == budgetId.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Infocom infocom, CancellationToken ct = default)
    {
        await _context.Infocoms.AddAsync(infocom, ct);
    }

    public async Task UpdateAsync(Infocom infocom, CancellationToken ct = default)
    {
        _context.Infocoms.Update(infocom);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Infocoms.AnyAsync(i => i.Id == id, ct);
    }

    public async Task<bool> AssetHasInfocomAsync(Guid assetId, CancellationToken ct = default)
    {
        return await _context.Infocoms.AnyAsync(i => i.AssetId == assetId && i.IsActive, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
