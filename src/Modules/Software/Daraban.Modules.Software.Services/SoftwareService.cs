using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Data.Repositories;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Modules.Software.Services.Interfaces;
using Daraban.Platform.Common;

namespace Daraban.Modules.Software.Services;

public class SoftwareService : ISoftwareService
{
    private readonly ISoftwareRepository _softwareRepository;

    public SoftwareService(ISoftwareRepository softwareRepository)
    {
        _softwareRepository = softwareRepository;
    }

    public async Task<Result<SoftwarePagedResult>> GetPagedAsync(
        Guid entityNodeId,
        string? search,
        SoftwareCategory? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var (items, totalCount) = await _softwareRepository.GetPagedAsync(
            entityNodeId, search, category, isActive, page, pageSize, ct);

        var dtos = items.Select(MapToListDto).ToList();
        return Result<SoftwarePagedResult>.Success(new SoftwarePagedResult(dtos, totalCount, page, pageSize));
    }

    public async Task<Result<SoftwareDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var software = await _softwareRepository.GetByIdWithDetailsAsync(id, ct);
        if (software is null)
            return Result.Failure<SoftwareDto>(new Error("SOFTWARE.NOT_FOUND", "Software not found.", ErrorType.NotFound));

        return Result<SoftwareDto>.Success(MapToDto(software));
    }

    public async Task<Result<SoftwareDto>> CreateAsync(CreateSoftwareRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        // Validate unique name
        var nameExists = await _softwareRepository.NameExistsAsync(request.Name, request.EntityNodeId, null, ct);
        if (nameExists)
            return Result.Failure<SoftwareDto>(new Error("SOFTWARE.NAME_EXISTS", "Software with this name already exists.", ErrorType.Conflict));

        var software = new Software
        {
            Id = Guid.NewGuid(),
            EntityId = request.EntityNodeId,
            Name = request.Name,
            Version = request.Version,
            Editor = request.Editor,
            Description = request.Description,
            Category = request.Category,
            Edition = request.Edition,
            IsActive = true,
            IsOpenSource = request.IsOpenSource,
            IsFree = request.IsFree,
            Website = request.Website,
            DocumentationUrl = request.DocumentationUrl,
            Comment = request.Comment,
            CreatedById = actorUserId,
            UpdatedById = actorUserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _softwareRepository.AddAsync(software, ct);
        await _softwareRepository.SaveChangesAsync(ct);

        return Result<SoftwareDto>.Success(MapToDto(software));
    }

    public async Task<Result<SoftwareDto>> UpdateAsync(Guid id, UpdateSoftwareRequest request, Guid actorUserId, CancellationToken ct = default)
    {
        var software = await _softwareRepository.GetByIdAsync(id, ct);
        if (software is null)
            return Result.Failure<SoftwareDto>(new Error("SOFTWARE.NOT_FOUND", "Software not found.", ErrorType.NotFound));

        // Validate unique name (excluding current software)
        var nameExists = await _softwareRepository.NameExistsAsync(request.Name, software.EntityId, id, ct);
        if (nameExists)
            return Result.Failure<SoftwareDto>(new Error("SOFTWARE.NAME_EXISTS", "Software with this name already exists.", ErrorType.Conflict));

        software.Name = request.Name;
        software.Version = request.Version;
        software.Editor = request.Editor;
        software.Description = request.Description;
        software.Category = request.Category;
        software.Edition = request.Edition;
        software.IsActive = request.IsActive;
        software.IsOpenSource = request.IsOpenSource;
        software.IsFree = request.IsFree;
        software.Website = request.Website;
        software.DocumentationUrl = request.DocumentationUrl;
        software.Comment = request.Comment;
        software.UpdatedAt = DateTimeOffset.UtcNow;
        software.UpdatedById = actorUserId;

        await _softwareRepository.UpdateAsync(software, ct);
        await _softwareRepository.SaveChangesAsync(ct);

        return Result<SoftwareDto>.Success(MapToDto(software));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorUserId, CancellationToken ct = default)
    {
        var software = await _softwareRepository.GetByIdAsync(id, ct);
        if (software is null)
            return Result.Failure(new Error("SOFTWARE.NOT_FOUND", "Software not found.", ErrorType.NotFound));

        // Soft delete
        software.IsDeleted = true;
        software.DeletedAt = DateTimeOffset.UtcNow;
        software.UpdatedAt = DateTimeOffset.UtcNow;
        software.UpdatedById = actorUserId;

        await _softwareRepository.UpdateAsync(software, ct);
        await _softwareRepository.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static SoftwareDto MapToDto(Software software) => new(
        software.Id,
        software.EntityId,
        software.Name,
        software.Version,
        software.Editor,
        software.Description,
        software.Category,
        software.Edition,
        software.IsActive,
        software.IsOpenSource,
        software.IsFree,
        software.Website,
        software.DocumentationUrl,
        software.Comment,
        software.CreatedAt,
        software.UpdatedAt);

    private static SoftwareListDto MapToListDto(Software software) => new(
        software.Id,
        software.Name,
        software.Version,
        software.Editor,
        software.Category,
        software.IsActive);
}
