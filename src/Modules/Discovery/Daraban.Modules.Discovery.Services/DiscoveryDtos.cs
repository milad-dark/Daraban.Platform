using Daraban.Modules.Discovery.Data.Entities;

namespace Daraban.Modules.Discovery.Services;

// DiscoveryRange DTOs
public record CreateRangeRequest(
    string Name,
    string CidrRange,
    string? StartIp,
    string? EndIp,
    ScanType ScanType,
    Guid? SnmpCredentialId,
    int ScanIntervalHours = 0);

public record UpdateRangeRequest(
    string? Name,
    string? CidrRange,
    string? StartIp,
    string? EndIp,
    ScanType? ScanType,
    Guid? SnmpCredentialId,
    int? ScanIntervalHours,
    bool? IsActive);

public record RangeResponse(
    Guid Id,
    string Name,
    string CidrRange,
    string? StartIp,
    string? EndIp,
    ScanType ScanType,
    Guid? SnmpCredentialId,
    string? SnmpCredentialName,
    bool IsActive,
    int ScanIntervalHours,
    DateTimeOffset? LastScanAt,
    DateTimeOffset CreatedAt);

// DiscoveryScan DTOs
public record StartScanRequest(
    Guid RangeId,
    ScanType? ScanType = null);

public record ScanResponse(
    Guid Id,
    Guid RangeId,
    string RangeName,
    ScanStatus Status,
    ScanType ScanType,
    int DevicesFound,
    int IpsResponded,
    int TotalIps,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    TimeSpan? Duration,
    string? ErrorMessage,
    string? InitiatedBy);

// DiscoveredDevice DTOs
public record DeviceResponse(
    long Id,
    Guid ScanId,
    Guid RangeId,
    string IpAddress,
    string? MacAddress,
    string? Hostname,
    string? OsGuess,
    string? OsVersion,
    string? Vendor,
    string? Model,
    string? SerialNumber,
    string? OpenPorts,
    string? SysDescr,
    string? SysName,
    string? SysLocation,
    string? SysContact,
    long? SnmpUptime,
    int? PingMs,
    int? Ttl,
    bool AssetCreated,
    Guid? AssetId,
    DateTimeOffset DiscoveredAt,
    DateTimeOffset? LastSeenAt);

// SnmpCredential DTOs
public record CreateCredentialRequest(
    string Name,
    SnmpVersion Version,
    string? CommunityString,
    string? UserName,
    AuthProtocol AuthProtocol,
    string? AuthPassphrase,
    PrivProtocol PrivProtocol,
    string? PrivPassphrase);

public record UpdateCredentialRequest(
    string? Name,
    SnmpVersion? Version,
    string? CommunityString,
    string? UserName,
    AuthProtocol? AuthProtocol,
    string? AuthPassphrase,
    PrivProtocol? PrivProtocol,
    string? PrivPassphrase,
    bool? IsActive);

public record CredentialResponse(
    Guid Id,
    string Name,
    SnmpVersion Version,
    bool IsActive,
    DateTimeOffset CreatedAt);

// DiscoveryRule DTOs
public record CreateRuleRequest(
    string Name,
    string? Description,
    string FilterCriteria,
    MatchAction Action,
    string? AssetType,
    Guid? EntityId,
    string? Tag,
    bool NotifyOnCreate = false,
    int Priority = 0);

public record UpdateRuleRequest(
    string? Name,
    string? Description,
    string? FilterCriteria,
    MatchAction? Action,
    string? AssetType,
    Guid? EntityId,
    string? Tag,
    bool? NotifyOnCreate,
    int? Priority,
    bool? IsActive);

public record RuleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    int Priority,
    string FilterCriteria,
    MatchAction Action,
    string? AssetType,
    Guid? EntityId,
    string? Tag,
    bool NotifyOnCreate,
    int AssetsCreatedCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastExecutedAt,
    DateTimeOffset? LastMatchedAt);

// Dashboard DTOs
public record DiscoveryDashboardResponse(
    int TotalRanges,
    int ActiveRanges,
    int TotalScans,
    int CompletedScans,
    int FailedScans,
    int TotalDevices,
    int AssetsCreated,
    List<ScanResponse> RecentScans,
    List<DeviceResponse> RecentDevices);

// Paged result
public record PagedResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
