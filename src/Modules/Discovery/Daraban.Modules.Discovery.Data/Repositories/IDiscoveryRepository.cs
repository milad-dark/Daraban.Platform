using Daraban.Modules.Discovery.Data.Entities;

namespace Daraban.Modules.Discovery.Data.Repositories;

/// <summary>Repository interface for Discovery module data access (Task 5.1).</summary>
public interface IDiscoveryRepository
{
    // DiscoveryRange operations
    Task<DiscoveryRange?> GetRangeByIdAsync(Guid id, CancellationToken ct = default);
    Task<DiscoveryRange?> GetRangeByNameAsync(string name, CancellationToken ct = default);
    Task<List<DiscoveryRange>> GetAllRangesAsync(CancellationToken ct = default);
    Task<List<DiscoveryRange>> GetActiveRangesAsync(CancellationToken ct = default);
    Task<List<DiscoveryRange>> GetRangesForScheduledScanAsync(CancellationToken ct = default);
    Task AddRangeAsync(DiscoveryRange range, CancellationToken ct = default);
    Task UpdateRangeAsync(DiscoveryRange range, CancellationToken ct = default);
    Task DeleteRangeAsync(Guid id, CancellationToken ct = default);

    // DiscoveryScan operations
    Task<DiscoveryScan?> GetScanByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<DiscoveryScan>> GetScansByRangeIdAsync(Guid rangeId, int page, int pageSize, CancellationToken ct = default);
    Task<List<DiscoveryScan>> GetRecentScansAsync(int count, CancellationToken ct = default);
    Task<DiscoveryScan?> GetQueuedScanAsync(CancellationToken ct = default);
    Task AddScanAsync(DiscoveryScan scan, CancellationToken ct = default);
    Task UpdateScanAsync(DiscoveryScan scan, CancellationToken ct = default);
    Task UpdateScanCountsAsync(Guid scanId, int devicesFound, int ipsResponded, int totalIps, CancellationToken ct = default);

    // DiscoveredDevice operations
    Task<DiscoveredDevice?> GetDeviceByIdAsync(long id, CancellationToken ct = default);
    Task<DiscoveredDevice?> GetDeviceByIpAndRangeAsync(string ipAddress, Guid rangeId, CancellationToken ct = default);
    Task<DiscoveredDevice?> GetDeviceByMacAddressAsync(string macAddress, CancellationToken ct = default);
    Task<DiscoveredDevice?> GetDeviceByHostnameAsync(string hostname, CancellationToken ct = default);
    Task<List<DiscoveredDevice>> GetDevicesByScanIdAsync(Guid scanId, CancellationToken ct = default);
    Task<List<DiscoveredDevice>> GetDevicesByRangeIdAsync(Guid rangeId, CancellationToken ct = default);
    Task<List<DiscoveredDevice>> GetRecentDevicesAsync(int count, CancellationToken ct = default);
    Task<int> GetDeviceCountByRangeIdAsync(Guid rangeId, CancellationToken ct = default);
    Task AddDeviceAsync(DiscoveredDevice device, CancellationToken ct = default);
    Task AddDevicesAsync(IEnumerable<DiscoveredDevice> devices, CancellationToken ct = default);
    Task UpdateDeviceAsync(DiscoveredDevice device, CancellationToken ct = default);

    // SnmpCredential operations
    Task<SnmpCredential?> GetCredentialByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<SnmpCredential>> GetActiveCredentialsAsync(CancellationToken ct = default);
    Task AddCredentialAsync(SnmpCredential credential, CancellationToken ct = default);
    Task UpdateCredentialAsync(SnmpCredential credential, CancellationToken ct = default);
    Task DeleteCredentialAsync(Guid id, CancellationToken ct = default);

    // DiscoveryRule operations
    Task<DiscoveryRule?> GetRuleByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<DiscoveryRule>> GetActiveRulesAsync(CancellationToken ct = default);
    Task<List<DiscoveryRule>> GetMatchingRulesAsync(string? osGuess, string? vendor, string? model, CancellationToken ct = default);
    Task AddRuleAsync(DiscoveryRule rule, CancellationToken ct = default);
    Task UpdateRuleAsync(DiscoveryRule rule, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid id, CancellationToken ct = default);

    // Save changes
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
