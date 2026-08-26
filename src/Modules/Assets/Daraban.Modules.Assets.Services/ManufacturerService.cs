using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services;

public class ManufacturerService : IManufacturerService
{
    private readonly IManufacturerRepository _repository;

    public ManufacturerService(IManufacturerRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<ManufacturerDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var manufacturers = await _repository.GetAllAsync(ct);
        var dtos = manufacturers.Select(m => new ManufacturerDto(
            m.Id, m.Name, m.Website, m.SupportUrl, m.SupportPhone)).ToList();
        return Result.Success<IReadOnlyList<ManufacturerDto>>(dtos);
    }

    public async Task<Result<ManufacturerDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var manufacturer = await _repository.GetByIdAsync(id, ct);
        if (manufacturer is null)
            return Result.Failure<ManufacturerDto>(new Error("ASSETS.MANUFACTURER_NOT_FOUND", "Manufacturer not found.", ErrorType.NotFound));

        return Result.Success(new ManufacturerDto(
            manufacturer.Id, manufacturer.Name, manufacturer.Website, manufacturer.SupportUrl, manufacturer.SupportPhone));
    }

    public async Task<Result<ManufacturerDto>> CreateAsync(CreateManufacturerRequest request, CancellationToken ct = default)
    {
        var nameExists = await _repository.NameExistsAsync(request.Name, null, ct);
        if (nameExists)
            return Result.Failure<ManufacturerDto>(new Error("ASSETS.MANUFACTURER_NAME_DUPLICATE", "A manufacturer with this name already exists.", ErrorType.Conflict));

        var now = DateTimeOffset.UtcNow;
        var manufacturer = new Manufacturer
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Website = request.Website,
            SupportUrl = request.SupportUrl,
            SupportPhone = request.SupportPhone,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _repository.AddAsync(manufacturer, ct);
        await _repository.SaveChangesAsync(ct);

        return Result.Success(new ManufacturerDto(
            manufacturer.Id, manufacturer.Name, manufacturer.Website, manufacturer.SupportUrl, manufacturer.SupportPhone));
    }

    public async Task<Result<ManufacturerDto>> UpdateAsync(Guid id, CreateManufacturerRequest request, CancellationToken ct = default)
    {
        var manufacturer = await _repository.GetByIdAsync(id, ct);
        if (manufacturer is null)
            return Result.Failure<ManufacturerDto>(new Error("ASSETS.MANUFACTURER_NOT_FOUND", "Manufacturer not found.", ErrorType.NotFound));

        var nameExists = await _repository.NameExistsAsync(request.Name, id, ct);
        if (nameExists)
            return Result.Failure<ManufacturerDto>(new Error("ASSETS.MANUFACTURER_NAME_DUPLICATE", "A manufacturer with this name already exists.", ErrorType.Conflict));

        manufacturer.Name = request.Name;
        manufacturer.Website = request.Website;
        manufacturer.SupportUrl = request.SupportUrl;
        manufacturer.SupportPhone = request.SupportPhone;
        manufacturer.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.SaveChangesAsync(ct);

        return Result.Success(new ManufacturerDto(
            manufacturer.Id, manufacturer.Name, manufacturer.Website, manufacturer.SupportUrl, manufacturer.SupportPhone));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var manufacturer = await _repository.GetByIdAsync(id, ct);
        if (manufacturer is null)
            return Result.Failure(new Error("ASSETS.MANUFACTURER_NOT_FOUND", "Manufacturer not found.", ErrorType.NotFound));

        manufacturer.DeletedAt = DateTimeOffset.UtcNow;
        manufacturer.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
