using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Data.Repositories;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Modules.Software.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Services;

public class SoftwareLicenseService : ISoftwareLicenseService
{
    private readonly ISoftwareLicenseRepository _licenseRepository;
    private readonly ISoftwareInstallationRepository _installationRepository;

    public SoftwareLicenseService(
        ISoftwareLicenseRepository licenseRepository,
        ISoftwareInstallationRepository installationRepository)
    {
        _licenseRepository = licenseRepository;
        _installationRepository = installationRepository;
    }

    public async Task<Result<SoftwareLicensePagedResult>> GetPagedAsync(
        Guid entityNodeId,
        Guid? softwareId,
        LicenseType? type,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _licenseRepository.GetPagedAsync(
            entityNodeId, softwareId, type, isActive, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<SoftwareLicensePagedResult>.Success(new SoftwareLicensePagedResult(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<SoftwareLicenseDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var license = await _licenseRepository.GetByIdWithDetailsAsync(id, ct);
        if (license is null)
            return Result.Failure<SoftwareLicenseDto>(new Error("LICENSE.NOT_FOUND", "Software license not found.", ErrorType.NotFound));

        return Result<SoftwareLicenseDto>.Success(MapToDto(license));
    }

    public async Task<Result<IReadOnlyList<SoftwareLicenseDto>>> GetBySoftwareIdAsync(Guid softwareId, CancellationToken ct = default)
    {
        var licenses = await _licenseRepository.GetBySoftwareIdAsync(softwareId, ct);
        var dtos = licenses.Select(MapToDto).ToList();
        return Result<IReadOnlyList<SoftwareLicenseDto>>.Success(dtos);
    }

    public async Task<Result<SoftwareLicenseDto>> CreateAsync(CreateSoftwareLicenseRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var license = new SoftwareLicense
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityNodeId,
            SoftwareId = request.SoftwareId,
            Name = request.Name,
            LicenseKey = request.LicenseKey,
            Type = request.Type,
            Quantity = request.Quantity,
            PurchaseDate = request.PurchaseDate,
            ExpirationDate = request.ExpirationDate,
            AutoRenew = request.AutoRenew,
            PurchaseCost = request.PurchaseCost,
            Currency = request.Currency,
            SupplierId = request.SupplierId,
            ContractId = request.ContractId,
            Comment = request.Comment,
            IsActive = true,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _licenseRepository.AddAsync(license, ct);
        await _licenseRepository.SaveChangesAsync(ct);

        return Result<SoftwareLicenseDto>.Success(MapToDto(license));
    }

    public async Task<Result<SoftwareLicenseDto>> UpdateAsync(Guid id, UpdateSoftwareLicenseRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var license = await _licenseRepository.GetByIdAsync(id, ct);
        if (license is null)
            return Result.Failure<SoftwareLicenseDto>(new Error("LICENSE.NOT_FOUND", "Software license not found.", ErrorType.NotFound));

        license.Name = request.Name;
        license.LicenseKey = request.LicenseKey;
        license.Type = request.Type;
        license.Quantity = request.Quantity;
        license.PurchaseDate = request.PurchaseDate;
        license.ExpirationDate = request.ExpirationDate;
        license.AutoRenew = request.AutoRenew;
        license.PurchaseCost = request.PurchaseCost;
        license.Currency = request.Currency;
        license.SupplierId = request.SupplierId;
        license.ContractId = request.ContractId;
        license.Comment = request.Comment;
        license.IsActive = request.IsActive;
        license.UpdatedAt = DateTimeOffset.UtcNow;
        license.UpdatedById = actorUserId;

        await _licenseRepository.UpdateAsync(license, ct);
        await _licenseRepository.SaveChangesAsync(ct);

        return Result<SoftwareLicenseDto>.Success(MapToDto(license));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var license = await _licenseRepository.GetByIdAsync(id, ct);
        if (license is null)
            return Result.Failure(new Error("LICENSE.NOT_FOUND", "Software license not found.", ErrorType.NotFound));

        // Check if license is in use
        var activeCount = await _installationRepository.GetActiveCountByLicenseIdAsync(id, ct);
        if (activeCount > 0)
            return Result.Failure(new Error("LICENSE.IN_USE", "Cannot delete license that is currently in use.", ErrorType.BusinessRule));

        // Soft delete
        license.IsDeleted = true;
        license.DeletedAt = DateTimeOffset.UtcNow;
        license.UpdatedAt = DateTimeOffset.UtcNow;
        license.UpdatedById = actorUserId;

        await _licenseRepository.UpdateAsync(license, ct);
        await _licenseRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<LicenseComplianceResult>> CheckComplianceAsync(Guid id, CancellationToken ct = default)
    {
        var license = await _licenseRepository.GetByIdWithDetailsAsync(id, ct);
        if (license is null)
            return Result.Failure<LicenseComplianceResult>(new Error("LICENSE.NOT_FOUND", "Software license not found.", ErrorType.NotFound));

        var activeCount = await _installationRepository.GetActiveCountByLicenseIdAsync(id, ct);
        var isExpired = license.ExpirationDate.HasValue && license.ExpirationDate.Value < DateTimeOffset.UtcNow;
        var isCompliant = activeCount <= license.Quantity && !isExpired;

        return Result<LicenseComplianceResult>.Success(new LicenseComplianceResult(
            id,
            license.SoftwareId,
            license.Quantity,
            activeCount,
            license.Quantity - activeCount,
            license.ExpirationDate,
            isExpired,
            isCompliant));
    }

    private static SoftwareLicenseDto MapToDto(SoftwareLicense license) => new(
        license.Id,
        license.EntityId,
        license.SoftwareId,
        license.Software?.Name,
        license.Name,
        license.LicenseKey,
        license.Type,
        license.Quantity,
        license.UsedQuantity,
        license.AvailableQuantity,
        license.IsCompliant,
        license.PurchaseDate,
        license.ExpirationDate,
        license.AutoRenew,
        license.PurchaseCost,
        license.Currency,
        license.SupplierId,
        license.ContractId,
        license.Comment,
        license.IsActive,
        license.IsExpired,
        license.CreatedAt,
        license.UpdatedAt);

    private static SoftwareLicenseListDto MapToListDto(SoftwareLicense license) => new(
        license.Id,
        license.SoftwareId,
        license.Software?.Name,
        license.Name,
        license.Type,
        license.Quantity,
        license.UsedQuantity,
        license.IsCompliant,
        license.ExpirationDate,
        license.IsActive);
}
