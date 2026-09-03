using Daraban.Modules.Software.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Software.Data.Repositories;

public class SoftwareInstallationRepository : ISoftwareInstallationRepository
{
    private readonly SoftwareDbContext _context;

    public SoftwareInstallationRepository(SoftwareDbContext context)
    {
        _context = context;
    }

    public async Task<SoftwareInstallation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Installations.FindAsync(new object[] { id }, ct);
    }

    public async Task<(IReadOnlyList<SoftwareInstallation> Items, int TotalCount)> GetPagedAsync(
        Guid entityNodeId,
        Guid? softwareId,
        Guid? licenseId,
        Guid? assetId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _context.Installations
            .Include(i => i.Software)
            .Include(i => i.License)
            .Where(i => i.Software.EntityId == entityNodeId);

        if (softwareId.HasValue)
            query = query.Where(i => i.SoftwareId == softwareId.Value);

        if (licenseId.HasValue)
            query = query.Where(i => i.LicenseId == licenseId.Value);

        if (assetId.HasValue)
            query = query.Where(i => i.AssetId == assetId.Value);

        if (isActive.HasValue)
            query = query.Where(i => i.IsActive == isActive.Value);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(i => i.Software.Name)
            .ThenBy(i => i.InstalledDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<SoftwareInstallation>> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default)
    {
        return await _context.Installations
            .Include(i => i.Software)
            .Include(i => i.License)
            .Where(i => i.AssetId == assetId && i.IsActive)
            .OrderBy(i => i.Software.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SoftwareInstallation>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default)
    {
        return await _context.Installations
            .Include(i => i.License)
            .Where(i => i.SoftwareId == softwareId && i.IsActive)
            .OrderBy(i => i.InstalledDate)
            .ToListAsync(ct);
    }

    public async Task<int> GetActiveCountByLicenseIdAsync(Guid licenseId, CancellationToken ct = default)
    {
        return await _context.Installations
            .CountAsync(i => i.LicenseId == licenseId && i.IsActive, ct);
    }

    public async Task AddAsync(SoftwareInstallation installation, CancellationToken ct = default)
    {
        await _context.Installations.AddAsync(installation, ct);
    }

    public async Task UpdateAsync(SoftwareInstallation installation, CancellationToken ct = default)
    {
        _context.Installations.Update(installation);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Installations.AnyAsync(i => i.Id == id, ct);
    }

    public async Task<bool> AssetHasInstallationAsync(Guid assetId, Guid softwareId, CancellationToken ct = default)
    {
        return await _context.Installations
            .AnyAsync(i => i.AssetId == assetId && i.SoftwareId == softwareId && i.IsActive, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
