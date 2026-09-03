using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Financial.Data.Repositories;

public class SupplierRepository : ISupplierRepository
{
    private readonly FinancialDbContext _context;

    public SupplierRepository(FinancialDbContext context)
    {
        _context = context;
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Suppliers.FindAsync(new object[] { id }, ct);
    }

    public async Task<Supplier?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Suppliers
            .Include(s => s.Contracts)
            .Include(s => s.Purchases)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<(IReadOnlyList<Supplier> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        SupplierType? type,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Suppliers
            .Where(s => s.EntityId == entityNodeId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || (s.ContactName != null && s.ContactName.Contains(search)));

        if (type.HasValue)
            query = query.Where(s => s.Type == type.Value);

        if (isActive.HasValue)
            query = query.Where(s => s.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task AddAsync(Supplier supplier, CancellationToken ct = default)
    {
        await _context.Suppliers.AddAsync(supplier, ct);
    }

    public async Task UpdateAsync(Supplier supplier, CancellationToken ct = default)
    {
        _context.Suppliers.Update(supplier);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Suppliers.AnyAsync(s => s.Id == id, ct);
    }

    public async Task<bool> NameExistsAsync(string name, Guid entityNodeId, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _context.Suppliers
            .AnyAsync(s => s.Name == name && s.EntityId == entityNodeId && (excludeId == null || s.Id != excludeId), ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
