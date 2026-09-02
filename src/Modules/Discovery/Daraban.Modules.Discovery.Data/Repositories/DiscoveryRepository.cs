using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Discovery.Data.Repositories;

/// <summary>Repository implementation for Discovery module data access (Task 5.1).</summary>
public class DiscoveryRepository(DiscoveryDbContext context) : IDiscoveryRepository
{

    // DiscoveryRange operations
    public async Task<DiscoveryRange?> GetRangeByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.DiscoveryRanges
            .Include(r => r.SnmpCredential)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<DiscoveryRange?> GetRangeByNameAsync(string name, CancellationToken ct = default)
    {
        return await context.DiscoveryRanges
            .FirstOrDefaultAsync(r => r.Name == name, ct);
    }

    public async Task<List<DiscoveryRange>> GetAllRangesAsync(CancellationToken ct = default)
    {
        return await context.DiscoveryRanges
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
    }

    public async Task<List<DiscoveryRange>> GetActiveRangesAsync(CancellationToken ct = default)
    {
        return await context.DiscoveryRanges
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
    }

    public async Task<List<DiscoveryRange>> GetRangesForScheduledScanAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await context.DiscoveryRanges
            .Where(r => r.IsActive && r.ScanIntervalHours > 0)
            .Where(r => r.LastScanAt == null || r.LastScanAt.Value.AddHours(r.ScanIntervalHours) <= now)
            .ToListAsync(ct);
    }

    public async Task AddRangeAsync(DiscoveryRange range, CancellationToken ct = default)
    {
        await context.DiscoveryRanges.AddAsync(range, ct);
    }

    public async Task UpdateRangeAsync(DiscoveryRange range, CancellationToken ct = default)
    {
        context.DiscoveryRanges.Update(range);
        await Task.CompletedTask;
    }

    public async Task DeleteRangeAsync(Guid id, CancellationToken ct = default)
    {
        var range = await context.DiscoveryRanges.FindAsync(new object[] { id }, ct);
        if (range != null)
        {
            // Check for dependent scans before deleting
            var hasScans = await context.DiscoveryScans
                .AnyAsync(s => s.RangeId == id, ct);
            if (hasScans)
                throw new InvalidOperationException("Cannot delete range with existing scans.");

            context.DiscoveryRanges.Remove(range);
        }
    }

    // DiscoveryScan operations
    public async Task<DiscoveryScan?> GetScanByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.DiscoveryScans
            .Include(s => s.Range)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<List<DiscoveryScan>> GetScansByRangeIdAsync(Guid rangeId, int page, int pageSize, CancellationToken ct = default)
    {
        return await context.DiscoveryScans
            .Include(s => s.Range)
            .Where(s => s.RangeId == rangeId)
            .OrderByDescending(s => s.QueuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<List<DiscoveryScan>> GetRecentScansAsync(int count, CancellationToken ct = default)
    {
        return await context.DiscoveryScans
            .Include(s => s.Range)
            .OrderByDescending(s => s.QueuedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<DiscoveryScan?> GetQueuedScanAsync(CancellationToken ct = default)
    {
        return await context.DiscoveryScans
            .Include(s => s.Range)
            .Where(s => s.Status == ScanStatus.Queued)
            .OrderBy(s => s.QueuedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddScanAsync(DiscoveryScan scan, CancellationToken ct = default)
    {
        await context.DiscoveryScans.AddAsync(scan, ct);
    }

    public async Task UpdateScanAsync(DiscoveryScan scan, CancellationToken ct = default)
    {
        context.DiscoveryScans.Update(scan);
        await Task.CompletedTask;
    }

    public async Task UpdateScanCountsAsync(Guid scanId, int devicesFound, int ipsResponded, int totalIps, CancellationToken ct = default)
    {
        var scan = await context.DiscoveryScans.FindAsync(new object[] { scanId }, ct);
        if (scan != null)
        {
            scan.DevicesFound = devicesFound;
            scan.IpsResponded = ipsResponded;
            scan.TotalIps = totalIps;
        }
    }

    // DiscoveredDevice operations
    public async Task<DiscoveredDevice?> GetDeviceByIdAsync(long id, CancellationToken ct = default)
    {
        return await context.DiscoveredDevices
            .Include(d => d.Scan)
            .Include(d => d.Range)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<DiscoveredDevice?> GetDeviceByIpAndRangeAsync(string ipAddress, Guid rangeId, CancellationToken ct = default)
    {
        return await context.DiscoveredDevices
            .FirstOrDefaultAsync(d => d.IpAddress == ipAddress && d.RangeId == rangeId, ct);
    }

    public async Task<DiscoveredDevice?> GetDeviceByMacAddressAsync(string macAddress, CancellationToken ct = default)
    {
        return await context.DiscoveredDevices
            .FirstOrDefaultAsync(d => d.MacAddress == macAddress, ct);
    }

    public async Task<DiscoveredDevice?> GetDeviceByHostnameAsync(string hostname, CancellationToken ct = default)
    {
        return await context.DiscoveredDevices
            .FirstOrDefaultAsync(d => d.Hostname == hostname, ct);
    }

    public async Task<List<DiscoveredDevice>> GetDevicesByScanIdAsync(Guid scanId, CancellationToken ct = default)
    {
        return await context.DiscoveredDevices
            .Where(d => d.ScanId == scanId)
            .OrderBy(d => d.IpAddress)
            .ToListAsync(ct);
    }

    public async Task<List<DiscoveredDevice>> GetDevicesByRangeIdAsync(Guid rangeId, CancellationToken ct = default)
    {
        return await context.DiscoveredDevices
            .Where(d => d.RangeId == rangeId)
            .OrderBy(d => d.IpAddress)
            .ToListAsync(ct);
    }

    public async Task<List<DiscoveredDevice>> GetRecentDevicesAsync(int count, CancellationToken ct = default)
    {
        return await context.DiscoveredDevices
            .Include(d => d.Scan)
            .Include(d => d.Range)
            .OrderByDescending(d => d.DiscoveredAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<int> GetDeviceCountByRangeIdAsync(Guid rangeId, CancellationToken ct = default)
    {
        return await context.DiscoveredDevices
            .CountAsync(d => d.RangeId == rangeId, ct);
    }

    public async Task AddDeviceAsync(DiscoveredDevice device, CancellationToken ct = default)
    {
        await context.DiscoveredDevices.AddAsync(device, ct);
    }

    public async Task AddDevicesAsync(IEnumerable<DiscoveredDevice> devices, CancellationToken ct = default)
    {
        await context.DiscoveredDevices.AddRangeAsync(devices, ct);
    }

    public async Task UpdateDeviceAsync(DiscoveredDevice device, CancellationToken ct = default)
    {
        context.DiscoveredDevices.Update(device);
        await Task.CompletedTask;
    }

    // SnmpCredential operations
    public async Task<SnmpCredential?> GetCredentialByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.SnmpCredentials.FindAsync(new object[] { id }, ct);
    }

    public async Task<List<SnmpCredential>> GetActiveCredentialsAsync(CancellationToken ct = default)
    {
        return await context.SnmpCredentials
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task AddCredentialAsync(SnmpCredential credential, CancellationToken ct = default)
    {
        await context.SnmpCredentials.AddAsync(credential, ct);
    }

    public async Task UpdateCredentialAsync(SnmpCredential credential, CancellationToken ct = default)
    {
        context.SnmpCredentials.Update(credential);
        await Task.CompletedTask;
    }

    public async Task DeleteCredentialAsync(Guid id, CancellationToken ct = default)
    {
        var credential = await context.SnmpCredentials.FindAsync(new object[] { id }, ct);
        if (credential != null)
        {
            // Check for dependent ranges before deleting
            var hasRanges = await context.DiscoveryRanges
                .AnyAsync(r => r.SnmpCredentialId == id, ct);
            if (hasRanges)
                throw new InvalidOperationException("Cannot delete credential that is in use by discovery ranges.");

            context.SnmpCredentials.Remove(credential);
        }
    }

    // DiscoveryRule operations
    public async Task<DiscoveryRule?> GetRuleByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.DiscoveryRules.FindAsync(new object[] { id }, ct);
    }

    public async Task<List<DiscoveryRule>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        return await context.DiscoveryRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);
    }

    public async Task<List<DiscoveryRule>> GetMatchingRulesAsync(string? osGuess, string? vendor, string? model, CancellationToken ct = default)
    {
        // Simple matching - in production, this should parse FilterCriteria JSON
        var rules = await context.DiscoveryRules
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

        return rules.Where(r => MatchesFilterCriteria(r.FilterCriteria, osGuess, vendor, model)).ToList();
    }

    private bool MatchesFilterCriteria(string filterCriteria, string? osGuess, string? vendor, string? model)
    {
        // Simple implementation - in production, use a proper JSON query engine
        // For now, if FilterCriteria is "{}" or empty, it matches everything
        if (string.IsNullOrWhiteSpace(filterCriteria) || filterCriteria == "{}")
            return true;

        // TODO: Implement proper JSON filter matching using System.Text.Json
        // Example: {"OsGuess": "Windows", "Vendor": "Dell"}
        // For now, return true as placeholder
        return true;
    }

    public async Task AddRuleAsync(DiscoveryRule rule, CancellationToken ct = default)
    {
        await context.DiscoveryRules.AddAsync(rule, ct);
    }

    public async Task UpdateRuleAsync(DiscoveryRule rule, CancellationToken ct = default)
    {
        context.DiscoveryRules.Update(rule);
        await Task.CompletedTask;
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await context.DiscoveryRules.FindAsync(new object[] { id }, ct);
        if (rule != null)
        {
            context.DiscoveryRules.Remove(rule);
        }
    }

    // Save changes
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await context.SaveChangesAsync(ct);
    }
}
