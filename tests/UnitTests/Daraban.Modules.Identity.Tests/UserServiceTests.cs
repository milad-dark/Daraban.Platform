using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Users;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// UserService: admin-side account management. The behaviours worth pinning are that a
/// disable/delete actually kills live sessions (via TokenVersion) and that duplicate
/// username/email return a conflict instead of letting the unique index throw.
/// </summary>
public class UserServiceTests
{
    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);

    private UserService CreateSut() => new(_users.Object);

    private static User UserWith(bool isActive = true, int tokenVersion = 0) => new()
    {
        Id = Guid.CreateVersion7(),
        Username = "alice",
        Email = "alice@example.com",
        DisplayName = "Alice",
        IsActive = isActive,
        TokenVersion = tokenVersion,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    private static CreateUserRequest CreateRequest(
        string username = "alice", string email = "alice@example.com")
        => new(username, email, "Alice", null);

    private static UpdateUserRequest UpdateRequest(
        string email = "alice@example.com", string displayName = "Alice Smith")
        => new(email, displayName, null);

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Username_With_A_Conflict()
    {
        _users.Setup(r => r.ExistsByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest());

        // Both columns carry unique indexes. Without this check the insert failed with a raw
        // DbUpdateException -- a 500 -- instead of a 409 naming the offending field.
        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.USERNAME_TAKEN", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Email_With_A_Conflict()
    {
        _users.Setup(r => r.ExistsByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.ExistsByEmailAsync("alice@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.EMAIL_TAKEN", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Checks_Uniqueness_Against_The_Trimmed_Values()
    {
        _users.Setup(r => r.ExistsByUsernameAsync("alice", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest("  alice  ", "  alice@example.com  "));

        Assert.False(result.IsSuccess);
        _users.Verify(r => r.ExistsByUsernameAsync("alice", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Leaves_The_Account_Without_A_Password()
    {
        User? captured = null;
        _users.Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u)
            .Returns(Task.CompletedTask);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest());

        Assert.True(result.IsSuccess);

        // An admin-provisioned account cannot be logged into until the user sets a password.
        // AuthService treats a null hash as a failed verification, so this is closed, not open.
        Assert.Null(captured!.PasswordHash);
        Assert.True(captured.IsActive);
        Assert.Equal(7, captured.Id.Version);
    }

    [Fact]
    public async Task CreateAsync_Trims_The_Stored_Values()
    {
        User? captured = null;
        _users.Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => captured = u)
            .Returns(Task.CompletedTask);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().CreateAsync(new CreateUserRequest("  bob ", " bob@example.com ", "  Bob  ", null));

        Assert.Equal("bob", captured!.Username);
        Assert.Equal("bob@example.com", captured.Email);
        Assert.Equal("Bob", captured.DisplayName);
    }

    // ---- Update ------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Returns_NotFound_For_A_Missing_User()
    {
        var id = Guid.CreateVersion7();
        _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateSut().UpdateAsync(id, UpdateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.USER_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Skips_The_Email_Check_When_The_Email_Is_Unchanged()
    {
        var user = UserWith();
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(user.Id, UpdateRequest("alice@example.com"));

        // Saving without changing the address must not collide with the row's own email. Strict
        // mocks prove the lookup never happened.
        Assert.True(result.IsSuccess);
        _users.Verify(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Compares_The_Existing_Email_Case_Insensitively()
    {
        var user = UserWith();
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(user.Id, UpdateRequest("ALICE@EXAMPLE.COM"));

        // Email is case-insensitive in practice, so re-casing it is not a change and must not
        // trigger a uniqueness check that would find the user's own row.
        Assert.True(result.IsSuccess);
        _users.Verify(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Moving_To_An_Email_Someone_Else_Holds()
    {
        var user = UserWith();
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.ExistsByEmailAsync("taken@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().UpdateAsync(user.Id, UpdateRequest("taken@example.com"));

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.EMAIL_TAKEN", result.Error!.Code);
        Assert.Equal("alice@example.com", user.Email);
    }

    [Fact]
    public async Task UpdateAsync_Cannot_Rename_The_Username()
    {
        var user = UserWith();
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateAsync(user.Id, UpdateRequest());

        // UpdateUserRequest deliberately has no Username field: it is the login identifier, and
        // renaming it silently breaks the user's own credentials plus every audit row that
        // recorded it.
        Assert.Equal("alice", user.Username);
    }

    [Fact]
    public async Task UpdateAsync_Does_Not_Touch_IsActive_Or_TokenVersion()
    {
        var user = UserWith(tokenVersion: 3);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateAsync(user.Id, UpdateRequest());

        // Editing a display name must not log the user out, and must not silently re-enable a
        // disabled account -- that is SetActiveAsync's job.
        Assert.True(user.IsActive);
        Assert.Equal(3, user.TokenVersion);
    }

    // ---- SetActive ---------------------------------------------------------------------------

    [Fact]
    public async Task SetActiveAsync_Disabling_Bumps_TokenVersion_To_Kill_Live_Sessions()
    {
        var user = UserWith(isActive: true, tokenVersion: 4);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().SetActiveAsync(user.Id, isActive: false);

        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);

        // Host.Api's OnTokenValidated compares this against the JWT's token_version claim. Without
        // the bump, a disabled user keeps working until their access token expires.
        Assert.Equal(5, user.TokenVersion);
    }

    [Fact]
    public async Task SetActiveAsync_Disabling_Clears_Any_Lockout_State()
    {
        var user = UserWith(isActive: true);
        user.FailedLoginCount = 5;
        user.LockoutEndAt = DateTimeOffset.UtcNow.AddMinutes(10);

        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().SetActiveAsync(user.Id, isActive: false);

        // Otherwise re-enabling the account later would hand back a still-locked user.
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndAt);
    }

    [Fact]
    public async Task SetActiveAsync_Enabling_Does_Not_Bump_TokenVersion()
    {
        var user = UserWith(isActive: false, tokenVersion: 4);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().SetActiveAsync(user.Id, isActive: true);

        Assert.True(result.IsSuccess);
        Assert.True(user.IsActive);

        // Re-enabling grants access; it does not need to revoke anything.
        Assert.Equal(4, user.TokenVersion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetActiveAsync_Rejects_A_No_Op(bool isActive)
    {
        var user = UserWith(isActive: isActive, tokenVersion: 1);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateSut().SetActiveAsync(user.Id, isActive);

        // Without this, re-disabling an already-disabled user would bump TokenVersion again for no
        // reason -- pointless churn on a column every request compares against.
        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.USER_STATE_UNCHANGED", result.Error!.Code);
        Assert.Equal(1, user.TokenVersion);
    }

    [Fact]
    public async Task SetActiveAsync_Returns_NotFound_For_A_Missing_User()
    {
        var id = Guid.CreateVersion7();
        _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateSut().SetActiveAsync(id, isActive: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.USER_NOT_FOUND", result.Error!.Code);
    }

    // ---- Delete ------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_SoftDeletes_Deactivates_And_Revokes_Tokens()
    {
        var user = UserWith(isActive: true, tokenVersion: 2);
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(user.Id);

        Assert.True(result.IsSuccess);
        Assert.True(user.IsDeleted);
        Assert.NotNull(user.DeletedAt);

        // The soft-delete query filter hides the row from lookups, but an already-issued access
        // token would otherwise keep working right up to its expiry.
        Assert.False(user.IsActive);
        Assert.Equal(3, user.TokenVersion);
    }

    [Fact]
    public async Task DeleteAsync_Returns_NotFound_For_A_Missing_User()
    {
        var id = Guid.CreateVersion7();
        _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateSut().DeleteAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("IDENTITY.USER_NOT_FOUND", result.Error!.Code);
    }

    // ---- Read --------------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_User()
    {
        var id = Guid.CreateVersion7();
        _users.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task GetByIdAsync_Never_Exposes_The_Password_Hash()
    {
        var user = UserWith();
        user.PasswordHash = "AQAAAAIAAYagAAAAE-not-a-real-hash";
        _users.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateSut().GetByIdAsync(user.Id);

        Assert.True(result.IsSuccess);

        // UserResponse has no hash field at all -- this asserts the DTO shape, which is what keeps
        // a credential out of an API response by construction rather than by remembering to strip it.
        var properties = result.Value.GetType().GetProperties().Select(p => p.Name).ToArray();
        Assert.DoesNotContain("PasswordHash", properties);
    }

    [Theory]
    [InlineData(0, 25, 1, 25)]      // page below 1 is clamped up
    [InlineData(-3, 25, 1, 25)]
    [InlineData(2, 0, 2, 20)]       // pageSize below 1 falls back to the default
    [InlineData(1, 10_000, 1, 200)] // capped so one request cannot pull every user
    public async Task SearchAsync_Normalizes_Paging(
        int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        _users.Setup(r => r.SearchAsync(
                null, null, expectedPage, expectedPageSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<User>(), 0));

        var result = await CreateSut().SearchAsync(null, null, page, pageSize);

        // An unclamped page of 0 produces Skip(-pageSize), which Postgres rejects outright.
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPage, result.Value.Page);
        Assert.Equal(expectedPageSize, result.Value.PageSize);
    }

    [Fact]
    public async Task SearchAsync_Reports_The_Total_And_Computes_Page_Count()
    {
        _users.Setup(r => r.SearchAsync(null, "ali", 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { UserWith(), UserWith() }, 42));

        var result = await CreateSut().SearchAsync(null, "ali", 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value.TotalCount);
        Assert.Equal(3, result.Value.TotalPages); // ceil(42 / 20)
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_Passes_The_Entity_Filter_Through()
    {
        var entityId = Guid.CreateVersion7();
        _users.Setup(r => r.SearchAsync(entityId, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<User>(), 0));

        var result = await CreateSut().SearchAsync(entityId, null, 1, 20);

        Assert.True(result.IsSuccess);
        _users.Verify(r => r.SearchAsync(entityId, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
