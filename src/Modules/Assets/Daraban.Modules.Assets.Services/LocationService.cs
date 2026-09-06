using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Assets.Services;

public class LocationService : ILocationService
{
    private const int MaxTreeDepth = 32;

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
            return Result.Failure<LocationDto>(NotFound());

        return Result.Success(new LocationDto(
            location.Id, location.ParentId, location.Name, location.City, location.Country));
    }

    public async Task<Result<LocationDto>> CreateAsync(CreateLocationRequest request, CancellationToken ct = default)
    {
        if (request.ParentId is not null)
        {
            var parent = await _repository.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<LocationDto>(ParentNotFound());
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
            return Result.Failure<LocationDto>(NotFound());

        if (request.ParentId != location.ParentId && request.ParentId is not null)
        {
            // A building cannot be its own room.
            if (request.ParentId.Value == id)
                return Result.Failure<LocationDto>(Cycle());

            var parent = await _repository.GetByIdAsync(request.ParentId.Value, ct);
            if (parent is null)
                return Result.Failure<LocationDto>(ParentNotFound());

            // Deeper cycles (Building -> Floor -> Room, then move Building under Room) are caught
            // by walking the full tree in memory -- the location tree is small by nature.
            var all = await _repository.GetAllAsync(ct);
            if (WouldCreateCycle(all, id, request.ParentId.Value))
                return Result.Failure<LocationDto>(Cycle());
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
            return Result.Failure(NotFound());

        // Deleting a parent location strands its children on a row hidden by the query filter.
        if (await _repository.HasChildrenAsync(id, ct))
            return Result.Failure(new Error(
                "ASSETS.LOCATION_HAS_CHILDREN",
                "Cannot delete a location that still has child locations.", ErrorType.BusinessRule));

        // Assets filed here keep their LocationId; deleting the location would orphan them.
        if (await _repository.HasAssetsAsync(id, ct))
            return Result.Failure(new Error(
                "ASSETS.LOCATION_HAS_ASSETS",
                "Cannot delete a location that still has assets placed at it.", ErrorType.BusinessRule));

        var now = DateTimeOffset.UtcNow;
        location.DeletedAt = now;
        location.UpdatedAt = now;
        await _repository.SaveChangesAsync(ct);

        return Result.Success();
    }

    // ---- helpers ---------------------------------------------------------------------------

    private static bool WouldCreateCycle(IReadOnlyList<Location> all, Guid movedId, Guid newParentId)
    {
        var byId = all.ToDictionary(l => l.Id);
        var seen = new HashSet<Guid>();
        var currentId = newParentId;

        for (var depth = 0; depth < MaxTreeDepth && byId.TryGetValue(currentId, out var current); depth++)
        {
            if (current.Id == movedId)
                return true;

            // Guard against a pre-existing cycle in the stored data: stop instead of looping.
            if (!seen.Add(current.Id))
                return true;

            if (current.ParentId is null)
                return false;

            currentId = current.ParentId.Value;
        }

        return false;
    }

    private static Error NotFound()
        => new("ASSETS.LOCATION_NOT_FOUND", "Location not found.", ErrorType.NotFound);

    private static Error ParentNotFound()
        => new("ASSETS.LOCATION_NOT_FOUND", "Parent location not found.", ErrorType.NotFound);

    private static Error Cycle()
        => new("ASSETS.LOCATION_CYCLE",
            "Cannot move a location beneath itself or one of its own descendants.", ErrorType.BusinessRule);
}
