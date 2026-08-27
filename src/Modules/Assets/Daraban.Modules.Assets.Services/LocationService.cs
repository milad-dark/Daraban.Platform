using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services;

public class LocationService : ILocationService
{
    private readonly ILocationRepository _repository;

    public LocationService(ILocationRepository repository) => _repository = repository;

    public async Task<Result<IReadOnlyList<LocationDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var locations = await _repository.GetAllAsync(ct);
        var dtos = locations.Select(l => new LocationDto(
            l.Id, l.ParentId, l.Name, l.City, l.Country)).ToList();
        return Result.Success<IReadOnlyList<LocationDto>>(dtos);
    }

    public async Task<Result<LocationDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var location = await _repository.GetByIdAsync(id, ct);
        if (location is null)
            return Result.Failure<LocationDto>(new Error("ASSETS.LOCATION_NOT_FOUND", "Location not found.", ErrorType.NotFound));

        return Result.Success(new LocationDto(
            location.Id, location.ParentId, location.Name, location.City, location.Country));
    }

    public async Task<Result<LocationDto>> CreateAsync(CreateLocationRequest request, CancellationToken ct = default)
    {
        if (request.ParentId is not null)
        {
            var parent = await _repository.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<LocationDto>(new Error("ASSETS.LOCATION_NOT_FOUND", "Parent location not found.", ErrorType.NotFound));
        }

        var now = DateTimeOffset.UtcNow;
        var location = new Location
        {
            Id = Guid.CreateVersion7(),
            ParentId = request.ParentId,
            Name = request.Name,
            Address = request.Address,
            PostalCode = request.PostalCode,
            City = request.City,
            Country = request.Country,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _repository.AddAsync(location, ct);
        await _repository.SaveChangesAsync(ct);

        return Result.Success(new LocationDto(
            location.Id, location.ParentId, location.Name, location.City, location.Country));
    }

    public async Task<Result<LocationDto>> UpdateAsync(Guid id, CreateLocationRequest request, CancellationToken ct = default)
    {
        var location = await _repository.GetByIdAsync(id, ct);
        if (location is null)
            return Result.Failure<LocationDto>(new Error("ASSETS.LOCATION_NOT_FOUND", "Location not found.", ErrorType.NotFound));

        if (request.ParentId is not null && request.ParentId != id)
        {
            var parent = await _repository.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<LocationDto>(new Error("ASSETS.LOCATION_NOT_FOUND", "Parent location not found.", ErrorType.NotFound));
        }

        location.ParentId = request.ParentId;
        location.Name = request.Name;
        location.Address = request.Address;
        location.PostalCode = request.PostalCode;
        location.City = request.City;
        location.Country = request.Country;
        location.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.SaveChangesAsync(ct);

        return Result.Success(new LocationDto(
            location.Id, location.ParentId, location.Name, location.City, location.Country));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var location = await _repository.GetByIdAsync(id, ct);
        if (location is null)
            return Result.Failure(new Error("ASSETS.LOCATION_NOT_FOUND", "Location not found.", ErrorType.NotFound));

        location.DeletedAt = DateTimeOffset.UtcNow;
        location.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.SaveChangesAsync(ct);

        return Result.Success();
    }
}
