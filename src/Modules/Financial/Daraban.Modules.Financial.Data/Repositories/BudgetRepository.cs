using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Financial.Data.Repositories;

public class BudgetRepository : IBudgetRepository
{
    private readonly FinancialDbContext _context;

    public BudgetRepository(FinancialDbContext context)
    {
        _context = context;
    }

    public async Task<Budget?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Budgets.FindAsync(new object[] { id }, ct);
    }

    public async Task<Budget?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Budgets
            .Include(b => b.ChildBudgets)
            .Include(b => b.InfocomEntries)
            .Include(b => b.Purchases)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Budget> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Budgets
            .Where(b => b.EntityId == entityNodeId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(b => b.Name.Contains(search));

        if (isActive.HasValue)
            query = query.Where(b => b.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<Budget?> GetByNameAsync(string name, Guid entityNodeId, CancellationToken ct = default)
    {
        return await _context.Budgets
            .FirstOrDefaultAsync(b => b.Name == name && b.EntityId == entityNodeId, ct);
    }

    public async Task AddAsync(Budget budget, CancellationToken ct = default)
    {
        await _context.Budgets.AddAsync(budget, ct);
    }

    public async Task UpdateAsync(Budget budget, CancellationToken ct = default)
    {
        _context.Budgets.Update(budget);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Budgets.AnyAsync(b => b.Id == id, ct);
    }

    public async Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _context.Budgets
            .AnyAsync(b => b.Name == name && b.EntityId == entityNodeId && (excludeId == null || b.Id != excludeId), ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
