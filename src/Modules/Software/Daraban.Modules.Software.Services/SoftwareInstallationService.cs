using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Data.Repositories;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Modules.Software.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Services;

public class SoftwareInstallationService : ISoftwareInstallationService
{
    private readonly ISoftwareInstallationRepository _installationRepository;
    private readonly ISoftwareRepository _softwareRepository;
    private readonly ISoftwareLicenseRepository _licenseRepository;

    public SoftwareInstallationService(
        ISoftwareInstallationRepository installationRepository,
        ISoftwareRepository softwareRepository,
        ISoftwareLicenseRepository licenseRepository)
    {
        _installationRepository = installationRepository;
        _softwareRepository = softwareRepository;
        _licenseRepository = licenseRepository;
    }

    public async Task<Result<SoftwareInstallationPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        Guid? softwareId,
        Guid? licenseId,
        Guid? assetId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _installationRepository.GetPagedAsync(
            entityNodeId, softwareId, licenseId, assetId, isActive, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<SoftwareInstallationPagedResult>.Success(new SoftwareInstallationPagedResult(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<SoftwareInstallationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var installation = await _installationRepository.GetByIdAsync(id, ct);
        if (installation is null)
            return Result.Failure<SoftwareInstallationDto>(new Error("INSTALLATION.NOT_FOUND", "Software installation not found.", ErrorType.NotFound));

        return Result<SoftwareInstallationDto>.Success(MapToDto(installation));
    }

    public async Task<Result<IReadOnlyList<SoftwareInstallationDto>>> GetByAssetIdAsync(Guid assetId, CancellationToken ct = default)
    {
        var installations = await _installationRepository.GetByAssetIdAsync(assetId, ct);
        var dtos = installations.Select(MapToDto).ToList();
        return Result<IReadOnlyList<SoftwareInstallationDto>>.Success(dtos);
    }

    public async Task<Result<IReadOnlyList<SoftwareInstallationDto>>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default)
    {
        var installations = await _installationRepository.GetBySoftwareIdAsync(softwareId, ct);
        var dtos = installations.Select(MapToDto).ToList();
        return Result<IReadOnlyList<SoftwareInstallationDto>>.Success(dtos);
    }

    public async Task<Result<SoftwareInstallationDto>> CreateAsync(CreateSoftwareInstallationRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Validate software exists
        var softwareExists = await _softwareRepository.ExistsAsync(request.SoftwareId, ct);
        if (!softwareExists)
            return Result.Failure<SoftwareInstallationDto>(new Error("SOFTWARE.NOT_FOUND", "Software not found.", ErrorType.NotFound));

        // Validate license if provided
        if (request.LicenseId.HasValue)
        {
            var license = await _licenseRepository.GetByIdAsync(request.LicenseId.Value, ct);
            if (license is null)
                return Result.Failure<SoftwareInstallationDto>(new Error("LICENSE.NOT_FOUND", "Software license not found.", ErrorType.NotFound));

            // Check license compliance
            var activeCount = await _installationRepository.GetActiveCountByLicenseIdAsync(request.LicenseId.Value, ct);
            if (activeCount >= license.Quantity)
                return Result.Failure<SoftwareInstallationDto>(new Error("LICENSE.COMPLIANCE", "No available licenses for this software.", ErrorType.BusinessRule));
        }

        // Check if asset already has this software installed
        var alreadyInstalled = await _installationRepository.AssetHasInstallationAsync(request.AssetNodeId, request.SoftwareId, ct);
        if (alreadyInstalled)
            return Result.Failure<SoftwareInstallationDto>(new Error("INSTALLATION.ALREADY_EXISTS", "This software is already installed on this asset.", ErrorType.Conflict));

        var installation = new SoftwareInstallation
        {
            Id = Guid.NewGuid(),
            SoftwareId = request.SoftwareId,
            LicenseId = request.LicenseId,
            AssetId = request.AssetNodeId,
            InstalledVersion = request.InstalledVersion,
            InstalledDate = DateTimeOffset.UtcNow,
            InstallPath = request.InstallPath,
            Source = request.Source,
            Comment = request.Comment,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _installationRepository.AddAsync(installation, ct);
        await _installationRepository.SaveChangesAsync(ct);

        return Result<SoftwareInstallationDto>.Success(MapToDto(installation));
    }

    public async Task<Result> UninstallAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var installation = await _installationRepository.GetByIdAsync(id, ct);
        if (installation is null)
            return Result.Failure(new Error("INSTALLATION.NOT_FOUND", "Software installation not found.", ErrorType.NotFound));

        installation.IsActive = false;
        installation.UninstalledDate = DateTimeOffset.UtcNow;
        installation.UpdatedAt = DateTimeOffset.UtcNow;

        await _installationRepository.UpdateAsync(installation, ct);
        await _installationRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<AssetSoftwareSummaryDto>> GetAssetSummaryAsync(Guid assetId, CancellationToken ct = default)
    {
        var installations = await _installationRepository.GetByAssetIdAsync(assetId, ct);
        var totalCount = installations.Count;
        var totalInstallations = installations.Count(i => i.IsActive);
        var withLicense = installations.Count(i => i.LicenseId.HasValue);

        return Result<AssetSoftwareSummaryDto>.Success(new AssetSoftwareSummaryDto(
            assetId,
            totalCount,
            totalInstallations,
            withLicense));
    }

    private static SoftwareInstallationDto MapToDto(SoftwareInstallation installation) => new(
        installation.Id,
        installation.SoftwareId,
        installation.Software?.Name,
        installation.LicenseId,
        installation.License?.Name,
        installation.AssetId,
        installation.InstalledVersion,
        installation.InstalledDate,
        installation.UninstalledDate,
        installation.IsActive,
        installation.InstallPath,
        installation.Source,
        installation.Comment,
        installation.CreatedAt,
        installation.UpdatedAt);

    private static SoftwareInstallationListDto MapToListDto(SoftwareInstallation installation) => new(
        installation.Id,
        installation.SoftwareId,
        installation.Software?.Name,
        installation.LicenseId,
        installation.License?.Name,
        installation.AssetId,
        installation.InstalledVersion,
        installation.InstalledDate,
        installation.IsActive,
        installation.Source);
}
