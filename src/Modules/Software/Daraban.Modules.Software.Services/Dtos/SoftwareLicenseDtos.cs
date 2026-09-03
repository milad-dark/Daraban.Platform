using Daraban.Modules.Software.Data.Entities;

namespace Daraban.Modules.Software.Services.Dtos;

public record SoftwareLicenseDto(
    Guid Id,
    Guid EntityId,
    Guid SoftwareId,
    string? SoftwareName,
    string Name,
    string? LicenseKey,
    LicenseType Type,
    int Quantity,
    int UsedQuantity,
    int AvailableQuantity,
    bool IsCompliant,
    DateTimeOffset? PurchaseDate,
    DateTimeOffset? ExpirationDate,
    bool AutoRenew,
    decimal? PurchaseCost,
    string Currency,
    Guid? SupplierId,
    Guid? ContractId,
    string? Comment,
    bool IsActive,
    bool IsExpired,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record SoftwareLicenseListDto(
    Guid Id,
    Guid SoftwareId,
    string? SoftwareName,
    string Name,
    LicenseType Type,
    int Quantity,
    int UsedQuantity,
    bool IsCompliant,
    DateTimeOffset? ExpirationDate,
    bool IsActive);

public record SoftwareLicensePagedResult(
    IReadOnlyList<SoftwareLicenseListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record CreateSoftwareLicenseRequest(
    Guid EntityNodeId,
    Guid SoftwareId,
    string Name,
    string? LicenseKey,
    LicenseType Type,
    int Quantity,
    DateTimeOffset? PurchaseDate,
    DateTimeOffset? ExpirationDate,
    bool AutoRenew,
    decimal? PurchaseCost,
    string Currency,
    Guid? SupplierId,
    Guid? ContractId,
    string? Comment);

public record UpdateSoftwareLicenseRequest(
    string Name,
    string? LicenseKey,
    LicenseType Type,
    int Quantity,
    DateTimeOffset? PurchaseDate,
    DateTimeOffset? ExpirationDate,
    bool AutoRenew,
    decimal? PurchaseCost,
    string Currency,
    Guid? SupplierId,
    Guid? ContractId,
    string? Comment,
    bool IsActive);

public record LicenseComplianceResult(
    Guid LicenseId,
    Guid SoftwareId,
    int TotalLicenses,
    int InstalledCount,
    int AvailableCount,
    DateTimeOffset? ExpirationDate,
    bool IsExpired,
    bool IsCompliant);
