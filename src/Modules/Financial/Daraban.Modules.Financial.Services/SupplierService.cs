using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Modules.Financial.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Financial.Services;

public class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _supplierRepository;

    public SupplierService(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository;
    }

    public async Task<Result<SupplierPagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        SupplierType? type,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _supplierRepository.GetPagedAsync(
            entityNodeId, search, type, isActive, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<SupplierPagedResult>.Success(new SupplierPagedResult(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<SupplierDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var supplier = await _supplierRepository.GetByIdWithDetailsAsync(id, ct);
        if (supplier is null)
            return Result.Failure<SupplierDto>(new Error("SUPPLIER.NOT_FOUND", "Supplier not found.", ErrorType.NotFound));

        return Result<SupplierDto>.Success(MapToDto(supplier));
    }

    public async Task<Result<SupplierDto>> CreateAsync(CreateSupplierRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Validate unique name
        var nameExists = await _supplierRepository.NameExistsAsync(request.Name, request.EntityNodeId, null, ct);
        if (nameExists)
            return Result.Failure<SupplierDto>(new Error("SUPPLIER.NAME_EXISTS", "A supplier with this name already exists.", ErrorType.Conflict));

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityNodeId,
            Name = request.Name,
            TradingName = request.TradingName,
            ContactName = request.ContactName,
            Email = request.Email,
            Phone = request.Phone,
            Mobile = request.Mobile,
            Fax = request.Fax,
            Website = request.Website,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            RegistrationNumber = request.RegistrationNumber,
            VatNumber = request.VatNumber,
            Iban = request.Iban,
            BankName = request.BankName,
            SortCode = request.SortCode,
            Type = request.Type,
            Comment = request.Comment,
            IsActive = true,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _supplierRepository.AddAsync(supplier, ct);
        await _supplierRepository.SaveChangesAsync(ct);

        return Result<SupplierDto>.Success(MapToDto(supplier));
    }

    public async Task<Result<SupplierDto>> UpdateAsync(Guid id, UpdateSupplierRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id, ct);
        if (supplier is null)
            return Result.Failure<SupplierDto>(new Error("SUPPLIER.NOT_FOUND", "Supplier not found.", ErrorType.NotFound));

        // Validate unique name (excluding current supplier)
        var nameExists = await _supplierRepository.NameExistsAsync(request.Name, supplier.EntityId, id, ct);
        if (nameExists)
            return Result.Failure<SupplierDto>(new Error("SUPPLIER.NAME_EXISTS", "A supplier with this name already exists.", ErrorType.Conflict));

        supplier.Name = request.Name;
        supplier.TradingName = request.TradingName;
        supplier.ContactName = request.ContactName;
        supplier.Email = request.Email;
        supplier.Phone = request.Phone;
        supplier.Mobile = request.Mobile;
        supplier.Fax = request.Fax;
        supplier.Website = request.Website;
        supplier.AddressLine1 = request.AddressLine1;
        supplier.AddressLine2 = request.AddressLine2;
        supplier.City = request.City;
        supplier.State = request.State;
        supplier.PostalCode = request.PostalCode;
        supplier.Country = request.Country;
        supplier.RegistrationNumber = request.RegistrationNumber;
        supplier.VatNumber = request.VatNumber;
        supplier.Iban = request.Iban;
        supplier.BankName = request.BankName;
        supplier.SortCode = request.SortCode;
        supplier.Type = request.Type;
        supplier.Comment = request.Comment;
        supplier.IsActive = request.IsActive;
        supplier.UpdatedAt = DateTimeOffset.UtcNow;
        supplier.UpdatedById = actorUserId;

        await _supplierRepository.UpdateAsync(supplier, ct);
        await _supplierRepository.SaveChangesAsync(ct);

        return Result<SupplierDto>.Success(MapToDto(supplier));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var supplier = await _supplierRepository.GetByIdAsync(id, ct);
        if (supplier is null)
            return Result.Failure(new Error("SUPPLIER.NOT_FOUND", "Supplier not found.", ErrorType.NotFound));

        // Soft delete
        supplier.IsDeleted = true;
        supplier.DeletedAt = DateTimeOffset.UtcNow;
        supplier.UpdatedAt = DateTimeOffset.UtcNow;
        supplier.UpdatedById = actorUserId;

        await _supplierRepository.UpdateAsync(supplier, ct);
        await _supplierRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static SupplierDto MapToDto(Supplier supplier) => new(
        supplier.Id,
        supplier.EntityId,
        supplier.Name,
        supplier.TradingName,
        supplier.ContactName,
        supplier.Email,
        supplier.Phone,
        supplier.Mobile,
        supplier.Fax,
        supplier.Website,
        supplier.AddressLine1,
        supplier.AddressLine2,
        supplier.City,
        supplier.State,
        supplier.PostalCode,
        supplier.Country,
        supplier.RegistrationNumber,
        supplier.VatNumber,
        supplier.Iban,
        supplier.BankName,
        supplier.SortCode,
        supplier.Type,
        supplier.Comment,
        supplier.IsActive,
        supplier.CreatedAt,
        supplier.UpdatedAt);

    private static SupplierListDto MapToListDto(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.ContactName,
        supplier.Email,
        supplier.Phone,
        supplier.Type,
        supplier.IsActive);
}
