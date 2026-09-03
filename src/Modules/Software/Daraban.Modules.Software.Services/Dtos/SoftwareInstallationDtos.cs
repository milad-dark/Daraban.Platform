using Daraban.Modules.Software.Data.Entities;

namespace Daraban.Modules.Software.Services.Dtos;

public record SoftwareInstallationDto(
    Guid Id,
    Guid SoftwareId,
    string? SoftwareName,
    Guid? LicenseId,
    string? LicenseName,
    Guid AssetId,
    string? InstalledVersion,
    DateTimeOffset InstalledDate,
    DateTimeOffset? UninstalledDate,
    bool IsActive,
    string? InstallPath,
    InstallationSource Source,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record SoftwareInstallationListDto(
    Guid Id,
    Guid SoftwareId,
    string? SoftwareName,
    Guid? LicenseId,
    string? LicenseName,
    Guid AssetId,
    string? InstalledVersion,
    DateTimeOffset InstalledDate,
    bool IsActive,
    InstallationSource Source);

public record SoftwareInstallationPagedResult(
    IReadOnlyList<SoftwareInstallationListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record CreateSoftwareInstallationRequest(
    Guid SoftwareId,
    Guid? LicenseId,
    Guid AssetNodeId,
    string? InstalledVersion,
    string? InstallPath,
    InstallationSource Source,
    string? Comment);

public record AssetSoftwareSummaryDto(
    Guid AssetId,
    int TotalSoftware,
    int ActiveInstallations,
    int WithLicense);
