using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Services.Users;

public interface IUserService
{
    Task<Result<PagedList<UserResponse>>> SearchAsync(Guid? entityId, string? q, int page, int pageSize, CancellationToken ct = default);
    Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result<UserResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    /// <summary>
    /// Enables or disables an account. Disabling bumps TokenVersion, which invalidates every
    /// access token already issued to that user on their next request -- otherwise a disabled
    /// user keeps working for the remainder of their token's lifetime.
    /// </summary>
    Task<Result<UserResponse>> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

    /// <summary>Soft delete. Also deactivates and bumps TokenVersion so existing sessions die.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
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
        var (normalizedPage, normalizedPageSize) = NormalizePaging(page, pageSize);

        var (items, total) = await _repository.SearchAsync(entityId, q, normalizedPage, normalizedPageSize, ct);
        var dtos = items.Select(ToResponse).ToList();
        return Result.Success(new PagedList<UserResponse>(dtos, normalizedPage, normalizedPageSize, total));
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct);
        if (user is null)
            return Result.Failure<UserResponse>(NotFound());
        return Result.Success(ToResponse(user));
    }

    public async Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();

        // Username and email are both backed by unique indexes. Without these checks the insert
        // failed with a raw DbUpdateException (a 500), instead of a 409 naming the field.
        if (await _repository.ExistsByUsernameAsync(username, ct))
            return Result.Failure<UserResponse>(UsernameTaken());
        if (await _repository.ExistsByEmailAsync(email, ct))
            return Result.Failure<UserResponse>(EmailTaken());

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            Username = username,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            DefaultEntityId = request.DefaultEntityId,
            // No PasswordHash: an admin-provisioned account cannot be logged into until the user
            // sets a password. AuthService.LoginAsync already treats a null hash as a failed
            // verification, so this is closed rather than open.
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _repository.AddAsync(user, ct);
        await _repository.SaveChangesAsync(ct);
        return Result.Success(ToResponse(user));
    }

    public async Task<Result<UserResponse>> UpdateAsync(
        Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct);
        if (user is null)
            return Result.Failure<UserResponse>(NotFound());

        var email = request.Email.Trim();

        // Username is deliberately absent from UpdateUserRequest -- it is the login identifier and
        // renaming it silently breaks the user's own credentials plus every audit row that recorded
        // it. Email can change, so it is re-checked for uniqueness excluding this user.
        if (!string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase)
            && await _repository.ExistsByEmailAsync(email, ct))
        {
            return Result.Failure<UserResponse>(EmailTaken());
        }

        user.Email = email;
        user.DisplayName = request.DisplayName.Trim();
        user.DefaultEntityId = request.DefaultEntityId;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.SaveChangesAsync(ct);
        return Result.Success(ToResponse(user));
    }

    public async Task<Result<UserResponse>> SetActiveAsync(
        Guid id, bool isActive, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct);
        if (user is null)
            return Result.Failure<UserResponse>(NotFound());

        if (user.IsActive == isActive)
            return Result.Failure<UserResponse>(new Error(
                "IDENTITY.USER_STATE_UNCHANGED",
                $"User is already {(isActive ? "active" : "inactive")}.", ErrorType.BusinessRule));

        user.IsActive = isActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        if (!isActive)
        {
            // Host.Api's OnTokenValidated compares this against the JWT's token_version claim
            // (Task 1.3 SS8). Bumping it revokes every outstanding access token immediately;
            // without it a disabled user stays authenticated until their token expires.
            user.TokenVersion++;
            user.FailedLoginCount = 0;
            user.LockoutEndAt = null;
        }

        await _repository.SaveChangesAsync(ct);
        return Result.Success(ToResponse(user));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _repository.GetByIdAsync(id, ct);
        if (user is null)
            return Result.Failure(NotFound());

        var now = DateTimeOffset.UtcNow;
        user.IsDeleted = true;
        user.DeletedAt = now;
        // Deactivated and token-revoked as well as flagged deleted: the soft-delete query filter
        // hides the row from lookups, but an already-issued access token would otherwise keep
        // working right up to its expiry.
        user.IsActive = false;
        user.TokenVersion++;
        user.UpdatedAt = now;

        await _repository.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize)
        // An unclamped page of 0 produces Skip(-pageSize), which Postgres rejects outright.
        => (page < 1 ? 1 : page, pageSize switch { < 1 => 20, > 200 => 200, _ => pageSize });

    private static Error NotFound()
        => new("IDENTITY.USER_NOT_FOUND", "User not found.", ErrorType.NotFound);

    private static Error UsernameTaken()
        => new("IDENTITY.USERNAME_TAKEN", "That username is already in use.", ErrorType.Conflict);

    private static Error EmailTaken()
        => new("IDENTITY.EMAIL_TAKEN", "That email is already registered.", ErrorType.Conflict);

    private static UserResponse ToResponse(User u) => new(u.Id, u.Username, u.Email, u.DisplayName, u.IsActive);
}

// DTOs (Task 1.4 SS4) -- request/response shapes, never the EF entity itself.
public sealed record CreateUserRequest(string Username, string Email, string DisplayName, Guid? DefaultEntityId);

/// <summary>No Username: it is the login identifier, and renaming it would break the user's own
/// credentials and every audit row that recorded it.</summary>
public sealed record UpdateUserRequest(string Email, string DisplayName, Guid? DefaultEntityId);

public sealed record UserResponse(Guid Id, string Username, string Email, string DisplayName, bool IsActive);
