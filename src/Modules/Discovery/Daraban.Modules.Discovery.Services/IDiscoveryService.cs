using Daraban.Modules.Discovery.Data.Entities;

namespace Daraban.Modules.Discovery.Services;

/// <summary>
/// Manages network device discovery (Task 5.1).
/// Handles scan scheduling, execution, and device tracking.
/// </summary>
public interface IDiscoveryService
{
    // DiscoveryRange operations
    Task<RangeResponse> CreateRangeAsync(CreateRangeRequest request, string? createdBy, CancellationToken ct = default);
    Task<RangeResponse?> GetRangeByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<RangeResponse>> GetAllRangesAsync(CancellationToken ct = default);
    Task<List<RangeResponse>> GetActiveRangesAsync(CancellationToken ct = default);
    Task<RangeResponse> UpdateRangeAsync(Guid id, UpdateRangeRequest request, CancellationToken ct = default);
    Task DeleteRangeAsync(Guid id, CancellationToken ct = default);

    // DiscoveryScan operations
    Task<ScanResponse> StartScanAsync(StartScanRequest request, string? initiatedBy, CancellationToken ct = default);
    Task<ScanResponse?> GetScanByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<ScanResponse>> GetScansByRangeIdAsync(Guid rangeId, int page, int pageSize, CancellationToken ct = default);
    Task<List<ScanResponse>> GetRecentScansAsync(int count, CancellationToken ct = default);
    Task<ScanResponse?> GetQueuedScanAsync(CancellationToken ct = default);
    Task UpdateScanStatusAsync(Guid scanId, ScanStatus status, string? errorMessage = null, CancellationToken ct = default);
    Task UpdateScanCountsAsync(Guid scanId, int devicesFound, int ipsResponded, int totalIps, CancellationToken ct = default);

    // DiscoveredDevice operations
    Task<DeviceResponse?> GetDeviceByIdAsync(long id, CancellationToken ct = default);
    Task<List<DeviceResponse>> GetDevicesByScanIdAsync(Guid scanId, CancellationToken ct = default);
    Task<List<DeviceResponse>> GetDevicesByRangeIdAsync(Guid rangeId, CancellationToken ct = default);
    Task<List<DeviceResponse>> GetRecentDevicesAsync(int count, CancellationToken ct = default);
    Task AddDevicesAsync(Guid scanId, Guid rangeId, IEnumerable<DeviceResponse> devices, CancellationToken ct = default);

    // SnmpCredential operations
    Task<CredentialResponse> CreateCredentialAsync(CreateCredentialRequest request, string? createdBy, CancellationToken ct = default);
    Task<CredentialResponse?> GetCredentialByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CredentialResponse>> GetAllCredentialsAsync(CancellationToken ct = default);
    Task<CredentialResponse> UpdateCredentialAsync(Guid id, UpdateCredentialRequest request, CancellationToken ct = default);
    Task DeleteCredentialAsync(Guid id, CancellationToken ct = default);

    // DiscoveryRule operations
    Task<RuleResponse> CreateRuleAsync(CreateRuleRequest request, string? createdBy, CancellationToken ct = default);
    Task<RuleResponse?> GetRuleByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<RuleResponse>> GetAllRulesAsync(CancellationToken ct = default);
    Task<List<RuleResponse>> GetActiveRulesAsync(CancellationToken ct = default);
    Task<RuleResponse> UpdateRuleAsync(Guid id, UpdateRuleRequest request, CancellationToken ct = default);
    Task DeleteRuleAsync(Guid id, CancellationToken ct = default);

    // Dashboard
    Task<DiscoveryDashboardResponse> GetDashboardAsync(CancellationToken ct = default);

    // Scheduled scan support
    Task<List<RangeResponse>> GetRangesDueForScanAsync(CancellationToken ct = default);
    Task UpdateRangeLastScanTimeAsync(Guid rangeId, DateTimeOffset lastScanAt, CancellationToken ct = default);

    // SNMP Discovery (Task 5.2)
    Task<DeviceResponse?> DiscoverDeviceAsync(string ipAddress, Guid rangeId, Guid scanId, CancellationToken ct = default);
}
