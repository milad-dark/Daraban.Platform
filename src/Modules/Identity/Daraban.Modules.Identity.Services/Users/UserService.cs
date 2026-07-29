using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Services.Users;

public interface IUserService
{
    Task<Result<PagedList<UserResponse>>> SearchAsync(Guid? entityId, string? q, int page, int pageSize, CancellationToken ct = default);
    Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
}

/// <summary>Business logic as plain methods -- no Command/Query objects, no MediatR
/// dispatch (Task 1.1). Controllers call this directly.</summary>
public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    public UserService(IUserRepository repository) => _repository = repository;

    public async Task<Result<PagedList<UserResponse>>> SearchAsync(
        Guid? entityId, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await _repository.SearchAsync(entityId, q, page, pageSize, ct);
        var dtos = items.Select(ToResponse).ToList();
        return Result.Success(new PagedList<UserResponse>(dtos, page, pageSize, total));
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct);
        if (user is null)
            return Result.Failure<UserResponse>(new Error("IDENTITY.USER_NOT_FOUND", "User not found.", ErrorType.NotFound));
        return Result.Success(ToResponse(user));
    }

    public async Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Username = request.Username,
            Email = request.Email,
            DisplayName = request.DisplayName,
            DefaultEntityId = request.DefaultEntityId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await _repository.AddAsync(user, ct);
        await _repository.SaveChangesAsync(ct);
        return Result.Success(ToResponse(user));
    }

    private static UserResponse ToResponse(User u) => new(u.Id, u.Username, u.Email, u.DisplayName, u.IsActive);
}

// DTOs (Task 1.4 SS4) -- request/response shapes, never the EF entity itself.
public sealed record CreateUserRequest(string Username, string Email, string DisplayName, Guid? DefaultEntityId);
public sealed record UserResponse(Guid Id, string Username, string Email, string DisplayName, bool IsActive);
