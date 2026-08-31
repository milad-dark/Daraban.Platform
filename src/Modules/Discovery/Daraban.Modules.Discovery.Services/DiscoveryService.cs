using Daraban.Modules.Discovery.Data.Entities;
using Daraban.Modules.Discovery.Data.Repositories;

namespace Daraban.Modules.Discovery.Services;

/// <summary>
/// Service implementation for network device discovery (Task 5.1).
/// </summary>
public class DiscoveryService(IDiscoveryRepository repository, ICredentialEncryptionService encryptionService) : IDiscoveryService
{

    // DiscoveryRange operations
    public async Task<RangeResponse> CreateRangeAsync(CreateRangeRequest request, string? createdBy, CancellationToken ct = default)
    {
        var existing = await repository.GetRangeByNameAsync(request.Name, ct);
        if (existing != null)
            throw new InvalidOperationException($"Range with name '{request.Name}' already exists.");

        var range = new DiscoveryRange
        {
            Name = request.Name,
            CidrRange = request.CidrRange,
            StartIp = request.StartIp,
            EndIp = request.EndIp,
            ScanType = request.ScanType,
            SnmpCredentialId = request.SnmpCredentialId,
            ScanIntervalHours = request.ScanIntervalHours,
            CreatedBy = createdBy
        };

        await repository.AddRangeAsync(range, ct);
        await repository.SaveChangesAsync(ct);

        return MapToRangeResponse(range);
    }

    public async Task<RangeResponse?> GetRangeByIdAsync(Guid id, CancellationToken ct = default)
    {
        var range = await repository.GetRangeByIdAsync(id, ct);
        return range != null ? MapToRangeResponse(range) : null;
    }

    public async Task<List<RangeResponse>> GetAllRangesAsync(CancellationToken ct = default)
    {
        var ranges = await repository.GetAllRangesAsync(ct);
        return ranges.Select(MapToRangeResponse).ToList();
    }

    public async Task<List<RangeResponse>> GetActiveRangesAsync(CancellationToken ct = default)
    {
        var ranges = await repository.GetActiveRangesAsync(ct);
        return ranges.Select(MapToRangeResponse).ToList();
    }

    public async Task<RangeResponse> UpdateRangeAsync(Guid id, UpdateRangeRequest request, CancellationToken ct = default)
    {
        var range = await repository.GetRangeByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Range with ID '{id}' not found.");

        if (request.Name != null)
            range.Name = request.Name;
        if (request.CidrRange != null)
            range.CidrRange = request.CidrRange;
        if (request.StartIp != null)
            range.StartIp = request.StartIp;
        if (request.EndIp != null)
            range.EndIp = request.EndIp;
        if (request.ScanType.HasValue)
            range.ScanType = request.ScanType.Value;
        if (request.SnmpCredentialId.HasValue)
            range.SnmpCredentialId = request.SnmpCredentialId;
        if (request.ScanIntervalHours.HasValue)
            range.ScanIntervalHours = request.ScanIntervalHours.Value;
        if (request.IsActive.HasValue)
            range.IsActive = request.IsActive.Value;
        range.ModifiedAt = DateTimeOffset.UtcNow;

        await repository.UpdateRangeAsync(range, ct);
        await repository.SaveChangesAsync(ct);

        return MapToRangeResponse(range);
    }

    public async Task DeleteRangeAsync(Guid id, CancellationToken ct = default)
    {
        await repository.DeleteRangeAsync(id, ct);
        await repository.SaveChangesAsync(ct);
    }

    // DiscoveryScan operations
    public async Task<ScanResponse> StartScanAsync(StartScanRequest request, string? initiatedBy, CancellationToken ct = default)
    {
        var range = await repository.GetRangeByIdAsync(request.RangeId, ct)
            ?? throw new InvalidOperationException($"Range with ID '{request.RangeId}' not found.");

        var scanType = request.ScanType ?? range.ScanType;

        var scan = new DiscoveryScan
        {
            RangeId = request.RangeId,
            ScanType = scanType,
            Status = ScanStatus.Queued,
            InitiatedBy = initiatedBy
        };

        await repository.AddScanAsync(scan, ct);
        await repository.SaveChangesAsync(ct);

        return MapToScanResponse(scan, range.Name);
    }

    public async Task<ScanResponse?> GetScanByIdAsync(Guid id, CancellationToken ct = default)
    {
        var scan = await repository.GetScanByIdAsync(id, ct);
        return scan != null ? MapToScanResponse(scan, scan.Range?.Name ?? "Unknown") : null;
    }

    public async Task<List<ScanResponse>> GetScansByRangeIdAsync(Guid rangeId, int page, int pageSize, CancellationToken ct = default)
    {
        var scans = await repository.GetScansByRangeIdAsync(rangeId, page, pageSize, ct);
        return scans.Select(s => MapToScanResponse(s, s.Range?.Name ?? "")).ToList();
    }

    public async Task<List<ScanResponse>> GetRecentScansAsync(int count, CancellationToken ct = default)
    {
        var scans = await repository.GetRecentScansAsync(count, ct);
        return scans.Select(s => MapToScanResponse(s, s.Range?.Name ?? "")).ToList();
    }

    public async Task<ScanResponse?> GetQueuedScanAsync(CancellationToken ct = default)
    {
        var scan = await repository.GetQueuedScanAsync(ct);
        return scan != null ? MapToScanResponse(scan, scan.Range?.Name ?? "") : null;
    }

    public async Task UpdateScanStatusAsync(Guid scanId, ScanStatus status, string? errorMessage = null, CancellationToken ct = default)
    {
        var scan = await repository.GetScanByIdAsync(scanId, ct)
            ?? throw new InvalidOperationException($"Scan with ID '{scanId}' not found.");

        scan.Status = status;
        if (errorMessage != null)
            scan.ErrorMessage = errorMessage;

        if (status == ScanStatus.Running)
            scan.StartedAt = DateTimeOffset.UtcNow;
        else if (status is ScanStatus.Completed or ScanStatus.Failed or ScanStatus.Cancelled)
            scan.CompletedAt = DateTimeOffset.UtcNow;

        await repository.UpdateScanAsync(scan, ct);
        await repository.SaveChangesAsync(ct);
    }

    public async Task UpdateScanCountsAsync(Guid scanId, int devicesFound, int ipsResponded, int totalIps, CancellationToken ct = default)
    {
        await repository.UpdateScanCountsAsync(scanId, devicesFound, ipsResponded, totalIps, ct);
        await repository.SaveChangesAsync(ct);
    }

    // DiscoveredDevice operations
    public async Task<DeviceResponse?> GetDeviceByIdAsync(long id, CancellationToken ct = default)
    {
        var device = await repository.GetDeviceByIdAsync(id, ct);
        return device != null ? MapToDeviceResponse(device) : null;
    }

    public async Task<List<DeviceResponse>> GetDevicesByScanIdAsync(Guid scanId, CancellationToken ct = default)
    {
        var devices = await repository.GetDevicesByScanIdAsync(scanId, ct);
        return devices.Select(MapToDeviceResponse).ToList();
    }

    public async Task<List<DeviceResponse>> GetDevicesByRangeIdAsync(Guid rangeId, CancellationToken ct = default)
    {
        var devices = await repository.GetDevicesByRangeIdAsync(rangeId, ct);
        return devices.Select(MapToDeviceResponse).ToList();
    }

    public async Task<List<DeviceResponse>> GetRecentDevicesAsync(int count, CancellationToken ct = default)
    {
        var devices = await repository.GetRecentDevicesAsync(count, ct);
        return devices.Select(MapToDeviceResponse).ToList();
    }

    public async Task AddDevicesAsync(Guid scanId, Guid rangeId, IEnumerable<DeviceResponse> deviceRequests, CancellationToken ct = default)
    {
        var devices = deviceRequests.Select(d => new DiscoveredDevice
        {
            ScanId = scanId,
            RangeId = rangeId,
            IpAddress = d.IpAddress,
            MacAddress = d.MacAddress,
            Hostname = d.Hostname,
            OsGuess = d.OsGuess,
            OsVersion = d.OsVersion,
            Vendor = d.Vendor,
            Model = d.Model,
            SerialNumber = d.SerialNumber,
            OpenPorts = d.OpenPorts,
            SysDescr = d.SysDescr,
            SysName = d.SysName,
            SysLocation = d.SysLocation,
            SysContact = d.SysContact,
            SnmpUptime = d.SnmpUptime,
            PingMs = d.PingMs,
            Ttl = d.Ttl
        }).ToList();

        await repository.AddDevicesAsync(devices, ct);
        await repository.SaveChangesAsync(ct);
    }

    // SnmpCredential operations
    public async Task<CredentialResponse> CreateCredentialAsync(CreateCredentialRequest request, string? createdBy, CancellationToken ct = default)
    {
        var credential = new SnmpCredential
        {
            Name = request.Name,
            Version = request.Version,
            CommunityString = request.CommunityString != null ? encryptionService.Encrypt(request.CommunityString) : null,
            UserName = request.UserName,
            AuthProtocol = request.AuthProtocol,
            AuthPassphrase = request.AuthPassphrase != null ? encryptionService.Encrypt(request.AuthPassphrase) : null,
            PrivProtocol = request.PrivProtocol,
            PrivPassphrase = request.PrivPassphrase != null ? encryptionService.Encrypt(request.PrivPassphrase) : null,
            CreatedBy = createdBy
        };

        await repository.AddCredentialAsync(credential, ct);
        await repository.SaveChangesAsync(ct);

        return MapToCredentialResponse(credential);
    }

    public async Task<CredentialResponse?> GetCredentialByIdAsync(Guid id, CancellationToken ct = default)
    {
        var credential = await repository.GetCredentialByIdAsync(id, ct);
        return credential != null ? MapToCredentialResponse(credential) : null;
    }

    public async Task<List<CredentialResponse>> GetAllCredentialsAsync(CancellationToken ct = default)
    {
        var credentials = await repository.GetActiveCredentialsAsync(ct);
        return credentials.Select(MapToCredentialResponse).ToList();
    }

    public async Task<CredentialResponse> UpdateCredentialAsync(Guid id, UpdateCredentialRequest request, CancellationToken ct = default)
    {
        var credential = await repository.GetCredentialByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Credential with ID '{id}' not found.");

        if (request.Name != null)
            credential.Name = request.Name;
        if (request.Version.HasValue)
            credential.Version = request.Version.Value;
        if (request.CommunityString != null)
            credential.CommunityString = encryptionService.Encrypt(request.CommunityString);
        if (request.UserName != null)
            credential.UserName = request.UserName;
        if (request.AuthProtocol.HasValue)
            credential.AuthProtocol = request.AuthProtocol.Value;
        if (request.AuthPassphrase != null)
            credential.AuthPassphrase = encryptionService.Encrypt(request.AuthPassphrase);
        if (request.PrivProtocol.HasValue)
            credential.PrivProtocol = request.PrivProtocol.Value;
        if (request.PrivPassphrase != null)
            credential.PrivPassphrase = encryptionService.Encrypt(request.PrivPassphrase);
        if (request.IsActive.HasValue)
            credential.IsActive = request.IsActive.Value;
        credential.ModifiedAt = DateTimeOffset.UtcNow;

        await repository.UpdateCredentialAsync(credential, ct);
        await repository.SaveChangesAsync(ct);

        return MapToCredentialResponse(credential);
    }

    public async Task DeleteCredentialAsync(Guid id, CancellationToken ct = default)
    {
        await repository.DeleteCredentialAsync(id, ct);
        await repository.SaveChangesAsync(ct);
    }

    // DiscoveryRule operations
    public async Task<RuleResponse> CreateRuleAsync(CreateRuleRequest request, string? createdBy, CancellationToken ct = default)
    {
        var rule = new DiscoveryRule
        {
            Name = request.Name,
            Description = request.Description,
            FilterCriteria = request.FilterCriteria,
            Action = request.Action,
            AssetType = request.AssetType,
            EntityId = request.EntityId,
            Tag = request.Tag,
            NotifyOnCreate = request.NotifyOnCreate,
            Priority = request.Priority,
            CreatedBy = createdBy
        };

        await repository.AddRuleAsync(rule, ct);
        await repository.SaveChangesAsync(ct);

        return MapToRuleResponse(rule);
    }

    public async Task<RuleResponse?> GetRuleByIdAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await repository.GetRuleByIdAsync(id, ct);
        return rule != null ? MapToRuleResponse(rule) : null;
    }

    public async Task<List<RuleResponse>> GetAllRulesAsync(CancellationToken ct = default)
    {
        var rules = await repository.GetActiveRulesAsync(ct);
        return rules.Select(MapToRuleResponse).ToList();
    }

    public async Task<List<RuleResponse>> GetActiveRulesAsync(CancellationToken ct = default)
    {
        var rules = await repository.GetActiveRulesAsync(ct);
        return rules.Select(MapToRuleResponse).ToList();
    }

    public async Task<RuleResponse> UpdateRuleAsync(Guid id, UpdateRuleRequest request, CancellationToken ct = default)
    {
        var rule = await repository.GetRuleByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Rule with ID '{id}' not found.");

        if (request.Name != null)
            rule.Name = request.Name;
        if (request.Description != null)
            rule.Description = request.Description;
        if (request.FilterCriteria != null)
            rule.FilterCriteria = request.FilterCriteria;
        if (request.Action.HasValue)
            rule.Action = request.Action.Value;
        if (request.AssetType != null)
            rule.AssetType = request.AssetType;
        if (request.EntityId.HasValue)
            rule.EntityId = request.EntityId;
        if (request.Tag != null)
            rule.Tag = request.Tag;
        if (request.NotifyOnCreate.HasValue)
            rule.NotifyOnCreate = request.NotifyOnCreate.Value;
        if (request.Priority.HasValue)
            rule.Priority = request.Priority.Value;
        if (request.IsActive.HasValue)
            rule.IsActive = request.IsActive.Value;
        rule.ModifiedAt = DateTimeOffset.UtcNow;

        await repository.UpdateRuleAsync(rule, ct);
        await repository.SaveChangesAsync(ct);

        return MapToRuleResponse(rule);
    }

    public async Task DeleteRuleAsync(Guid id, CancellationToken ct = default)
    {
        await repository.DeleteRuleAsync(id, ct);
        await repository.SaveChangesAsync(ct);
    }

    // Dashboard
    public async Task<DiscoveryDashboardResponse> GetDashboardAsync(CancellationToken ct = default)
    {
        // Execute all three queries in parallel for better performance
        var rangesTask = repository.GetActiveRangesAsync(ct);
        var scansTask = repository.GetRecentScansAsync(10, ct);
        var devicesTask = repository.GetRecentDevicesAsync(10, ct);

        await Task.WhenAll(rangesTask, scansTask, devicesTask);

        var ranges = rangesTask.Result;
        var scans = scansTask.Result;
        var devices = devicesTask.Result;

        return new DiscoveryDashboardResponse(
            TotalRanges: ranges.Count,
            ActiveRanges: ranges.Count(r => r.IsActive),
            TotalScans: scans.Count,
            CompletedScans: scans.Count(s => s.Status == ScanStatus.Completed),
            FailedScans: scans.Count(s => s.Status == ScanStatus.Failed),
            TotalDevices: devices.Count,
            AssetsCreated: devices.Count(d => d.AssetCreated),
            RecentScans: scans.Select(s => MapToScanResponse(s, s.Range?.Name ?? "")).ToList(),
            RecentDevices: devices.Select(MapToDeviceResponse).ToList()
        );
    }

    // Scheduled scan support
    public async Task<List<RangeResponse>> GetRangesDueForScanAsync(CancellationToken ct = default)
    {
        var ranges = await repository.GetRangesForScheduledScanAsync(ct);
        return ranges.Select(MapToRangeResponse).ToList();
    }

    public async Task UpdateRangeLastScanTimeAsync(Guid rangeId, DateTimeOffset lastScanAt, CancellationToken ct = default)
    {
        var range = await repository.GetRangeByIdAsync(rangeId, ct);
        if (range != null)
        {
            range.LastScanAt = lastScanAt;
            await repository.UpdateRangeAsync(range, ct);
            await repository.SaveChangesAsync(ct);
        }
    }

    // Mapping methods
    private RangeResponse MapToRangeResponse(DiscoveryRange range) =>
        new(
            range.Id,
            range.Name,
            range.CidrRange,
            range.StartIp,
            range.EndIp,
            range.ScanType,
            range.SnmpCredentialId,
            range.SnmpCredential?.Name,
            range.IsActive,
            range.ScanIntervalHours,
            range.LastScanAt,
            range.CreatedAt
        );

    private ScanResponse MapToScanResponse(DiscoveryScan scan, string rangeName) =>
        new(
            scan.Id,
            scan.RangeId,
            rangeName,
            scan.Status,
            scan.ScanType,
            scan.DevicesFound,
            scan.IpsResponded,
            scan.TotalIps,
            scan.QueuedAt,
            scan.StartedAt,
            scan.CompletedAt,
            scan.Duration,
            scan.ErrorMessage,
            scan.InitiatedBy
        );

    private DeviceResponse MapToDeviceResponse(DiscoveredDevice device) =>
        new(
            device.Id,
            device.ScanId,
            device.RangeId,
            device.IpAddress,
            device.MacAddress,
            device.Hostname,
            device.OsGuess,
            device.OsVersion,
            device.Vendor,
            device.Model,
            device.SerialNumber,
            device.OpenPorts,
            device.SysDescr,
            device.SysName,
            device.SysLocation,
            device.SysContact,
            device.SnmpUptime,
            device.PingMs,
            device.Ttl,
            device.AssetCreated,
            device.AssetId,
            device.DiscoveredAt,
            device.LastSeenAt
        );

    private CredentialResponse MapToCredentialResponse(SnmpCredential credential) =>
        new(
            credential.Id,
            credential.Name,
            credential.Version,
            credential.IsActive,
            credential.CreatedAt
        );

    private RuleResponse MapToRuleResponse(DiscoveryRule rule) =>
        new(
            rule.Id,
            rule.Name,
            rule.Description,
            rule.IsActive,
            rule.Priority,
            rule.FilterCriteria,
            rule.Action,
            rule.AssetType,
            rule.EntityId,
            rule.Tag,
            rule.NotifyOnCreate,
            rule.AssetsCreatedCount,
            rule.CreatedAt,
            rule.LastExecutedAt,
            rule.LastMatchedAt
        );
}
