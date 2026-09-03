using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Financial.Data.Repositories;

public class PurchaseRepository : IPurchaseRepository
{
    private readonly FinancialDbContext _context;

    public PurchaseRepository(FinancialDbContext context)
    {
        _context = context;
    }

    public async Task<Purchase?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Purchases.FindAsync(new object[] { id }, ct);
    }

    public async Task<Purchase?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Purchases
            .Include(p => p.Supplier)
            .Include(p => p.Budget)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Purchase> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        PurchaseStatus? status,
        Guid? supplierId,
        Guid? budgetId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Purchases
            .Where(p => p.EntityId == entityNodeId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.Contains(search) || p.OrderNumber.Contains(search));

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (supplierId.HasValue)
            query = query.Where(p => p.SupplierId == supplierId.Value);

        if (budgetId.HasValue)
            query = query.Where(p => p.BudgetId == budgetId.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Purchase?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        return await _context.Purchases
            .FirstOrDefaultAsync(p => p.OrderNumber == orderNumber, ct);
    }

    public async Task AddAsync(Purchase purchase, CancellationToken ct = default)
    {
        await _context.Purchases.AddAsync(purchase, ct);
    }

    public async Task UpdateAsync(Purchase purchase, CancellationToken ct = default)
    {
        _context.Purchases.Update(purchase);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Purchases.AnyAsync(p => p.Id == id, ct);
    }

    public async Task<bool> OrderNumberExistsAsync(string orderNumber, CancellationToken ct = default)
    {
        return await _context.Purchases.AnyAsync(p => p.OrderNumber == orderNumber, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
