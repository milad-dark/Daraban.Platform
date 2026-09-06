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
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);

        var (items, totalCount) = await _installationRepository.GetPagedAsync(
            entityNodeId, softwareId, licenseId, assetId, isActive, normalizedPage, normalizedPageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result.Success(new SoftwareInstallationPagedResult(dtos, totalCount, normalizedPage, normalizedPageSize));
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
        return Result.Success<IReadOnlyList<SoftwareInstallationDto>>(dtos);
    }

    public async Task<Result<IReadOnlyList<SoftwareInstallationDto>>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default)
    {
        var installations = await _installationRepository.GetBySoftwareIdAsync(softwareId, ct);
        var dtos = installations.Select(MapToDto).ToList();
        return Result.Success<IReadOnlyList<SoftwareInstallationDto>>(dtos);
    }

    public async Task<Result<SoftwareInstallationDto>> CreateAsync(CreateSoftwareInstallationRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Validate software exists
        var software = await _softwareRepository.GetByIdAsync(request.SoftwareId, ct);
        if (software is null)
            return Result.Failure<SoftwareInstallationDto>(new Error("SOFTWARE.NOT_FOUND", "Software not found.", ErrorType.NotFound));

        // Validate license if provided
        SoftwareLicense? license = null;
        if (request.LicenseId.HasValue)
        {
            license = await _licenseRepository.GetByIdAsync(request.LicenseId.Value, ct);
            if (license is null)
                return Result.Failure<SoftwareInstallationDto>(new Error("LICENSE.NOT_FOUND", "Software license not found.", ErrorType.NotFound));

            // The license must cover this product -- otherwise a seat from an unrelated license is
            // consumed and both products' compliance counts lie.
            if (license.SoftwareId != request.SoftwareId)
                return Result.Failure<SoftwareInstallationDto>(new Error(
                    "LICENSE.SOFTWARE_MISMATCH",
                    "License does not cover this software.", ErrorType.BusinessRule));

            // The license must live in the same tenant as the software it covers.
            if (license.EntityId != software.EntityId)
                return Result.Failure<SoftwareInstallationDto>(new Error(
                    "LICENSE.CROSS_ENTITY",
                    "License belongs to a different entity than the software.", ErrorType.Forbidden));

            if (!license.IsActive)
                return Result.Failure<SoftwareInstallationDto>(new Error(
                    "LICENSE.INACTIVE", "Cannot install against an inactive license.", ErrorType.BusinessRule));

            if (license.IsExpired)
                return Result.Failure<SoftwareInstallationDto>(new Error(
                    "LICENSE.EXPIRED", "Cannot install against an expired license.", ErrorType.BusinessRule));

            // Check license compliance
            var activeCount = await _installationRepository.GetActiveCountByLicenseIdAsync(request.LicenseId.Value, ct);
            if (activeCount >= license.Quantity)
                return Result.Failure<SoftwareInstallationDto>(new Error("LICENSE.COMPLIANCE", "No available licenses for this software.", ErrorType.BusinessRule));
        }

        // Check if asset already has this software installed
        var alreadyInstalled = await _installationRepository.AssetHasInstallationAsync(request.AssetNodeId, request.SoftwareId, ct);
        if (alreadyInstalled)
            return Result.Failure<SoftwareInstallationDto>(new Error("INSTALLATION.ALREADY_EXISTS", "This software is already installed on this asset.", ErrorType.Conflict));

        var now = DateTimeOffset.UtcNow;
        var installation = new SoftwareInstallation
        {
            Id = Guid.CreateVersion7(),
            SoftwareId = request.SoftwareId,
            LicenseId = request.LicenseId,
            AssetId = request.AssetNodeId,
            InstalledVersion = request.InstalledVersion,
            InstalledDate = now,
            InstallPath = request.InstallPath,
            Source = request.Source,
            Comment = request.Comment,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _installationRepository.AddAsync(installation, ct);

        // Keep the license's denormalized UsedQuantity truthful -- the compliance DTOs and the
        // entity-level IsCompliant read it, and nothing else ever writes it.
        if (license is not null)
        {
            license.UsedQuantity = await _installationRepository.GetActiveCountByLicenseIdAsync(license.Id, ct) + 1;
            await _licenseRepository.UpdateAsync(license, ct);
        }

        // Both repositories share one scoped DbContext, so one SaveChanges commits the
        // installation row and the seat-counter together. Saving through the license repository
        // when no license was touched would issue a pointless second flush.
        await _installationRepository.SaveChangesAsync(ct);

        return Result.Success(MapToDto(installation));
    }

    public async Task<Result> UninstallAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var installation = await _installationRepository.GetByIdAsync(id, ct);
        if (installation is null)
            return Result.Failure(new Error("INSTALLATION.NOT_FOUND", "Software installation not found.", ErrorType.NotFound));

        if (!installation.IsActive)
            return Result.Failure(new Error(
                "INSTALLATION.ALREADY_UNINSTALLED", "Software is already uninstalled.", ErrorType.BusinessRule));

        installation.IsActive = false;
        installation.UninstalledDate = DateTimeOffset.UtcNow;
        installation.UpdatedAt = DateTimeOffset.UtcNow;

        await _installationRepository.UpdateAsync(installation, ct);

        // Release the seat back to the license, floored at zero so a historical inconsistency can
        // never drive the counter negative.
        if (installation.LicenseId.HasValue)
        {
            var license = await _licenseRepository.GetByIdAsync(installation.LicenseId.Value, ct);
            if (license is not null)
            {
                license.UsedQuantity = Math.Max(0, license.UsedQuantity - 1);
                await _licenseRepository.UpdateAsync(license, ct);
            }
        }

        // One shared DbContext: one SaveChanges flushes the deactivation and the seat release.
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

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
        => (page < 1 ? 1 : page, pageSize switch { < 1 => 20, > 200 => 200, _ => pageSize });
}
