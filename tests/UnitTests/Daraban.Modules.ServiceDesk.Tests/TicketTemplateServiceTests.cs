using Daraban.Modules.ServiceDesk.Data.Entities;
using Daraban.Modules.ServiceDesk.Data.Repositories;
using Daraban.Modules.ServiceDesk.Services;
using Daraban.Modules.ServiceDesk.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.ServiceDesk.Tests;

/// <summary>
/// TicketTemplateService: per-tenant name uniqueness, and the includeInactive flag that makes a
/// deactivated template findable again.
/// </summary>
public class TicketTemplateServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<ITicketTemplateRepository> _templates = new(MockBehavior.Strict);

    private TicketTemplateService CreateSut() => new(_templates.Object);

    private static TicketTemplate Template(string name = "Password Reset", bool isActive = true) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = EntityId,
        Name = name,
        DefaultType = TicketType.Request,
        DefaultPriority = TicketPriority.Low,
        DefaultImpact = TicketImpact.Low,
        DefaultUrgency = TicketUrgency.Low,
        IsActive = isActive,
    };

    private static CreateTicketTemplateRequest CreateRequest(string name = "Password Reset")
        => new(name, "For self-service password resets",
            TicketType.Request, TicketPriority.Low, TicketImpact.Low, TicketUrgency.Low,
            "Password reset for {username}", "User cannot sign in.", null, null, null, 5);

    private static UpdateTicketTemplateRequest UpdateRequest(
        string name = "Password Reset", bool isActive = true)
        => new(name, "Updated description",
            TicketType.Request, TicketPriority.Medium, TicketImpact.Low, TicketUrgency.Low,
            null, null, null, null, null, isActive, 3);

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_An_Empty_Entity()
    {
        var result = await CreateSut().CreateAsync(CreateRequest(), Guid.Empty, ActorId);

        // A template with no tenant would be invisible to every list query, which filters on
        // EntityId -- and its name uniqueness check would be meaningless.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TEMPLATE.ENTITY_REQUIRED", result.Error!.Code);
        _templates.Verify(r => r.AddAsync(It.IsAny<TicketTemplate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Name_Within_The_Tenant()
    {
        _templates.Setup(r => r.NameExistsAsync("Password Reset", EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest(), EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TEMPLATE.NAME_EXISTS", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Contains("Password Reset", result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_Persists_Every_Default_And_Starts_Active()
    {
        TicketTemplate? captured = null;

        _templates.Setup(r => r.NameExistsAsync("Password Reset", EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _templates.Setup(r => r.AddAsync(It.IsAny<TicketTemplate>(), It.IsAny<CancellationToken>()))
            .Callback<TicketTemplate, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);
        _templates.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest(), EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(EntityId, captured!.EntityId);
        Assert.True(captured.IsActive);
        Assert.Equal(5, captured.SortOrder);
        Assert.Equal("Password reset for {username}", captured.TitleTemplate);
        Assert.Equal(ActorId, captured.CreatedById);

        // UUIDv7 rather than Guid.NewGuid()'s v4 -- random ids scatter inserts across a table
        // clustered by primary key.
        Assert.Equal(7, captured.Id.Version);
    }

    // ---- Update ------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Returns_NotFound_For_A_Missing_Template()
    {
        var id = Guid.CreateVersion7();
        _templates.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((TicketTemplate?)null);

        var result = await CreateSut().UpdateAsync(id, UpdateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TEMPLATE.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Excludes_Itself_From_The_Name_Check()
    {
        var template = Template();

        _templates.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _templates.Setup(r => r.NameExistsAsync("Password Reset", EntityId, template.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _templates.Setup(r => r.UpdateAsync(template, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _templates.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(template.Id, UpdateRequest(), ActorId);

        // Saving without renaming must not collide with the row's own name.
        Assert.True(result.IsSuccess);
        _templates.Verify(r => r.NameExistsAsync(
            "Password Reset", EntityId, template.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Renaming_Onto_Another_Template()
    {
        var template = Template();

        _templates.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _templates.Setup(r => r.NameExistsAsync("New User Onboarding", EntityId, template.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().UpdateAsync(
            template.Id, UpdateRequest("New User Onboarding"), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TEMPLATE.NAME_EXISTS", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Can_Deactivate_And_Reactivate()
    {
        var template = Template(isActive: true);

        _templates.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _templates.Setup(r => r.NameExistsAsync(It.IsAny<string>(), EntityId, template.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _templates.Setup(r => r.UpdateAsync(template, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _templates.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var off = await CreateSut().UpdateAsync(template.Id, UpdateRequest(isActive: false), ActorId);
        Assert.True(off.IsSuccess);
        Assert.False(template.IsActive);

        var on = await CreateSut().UpdateAsync(template.Id, UpdateRequest(isActive: true), ActorId);
        Assert.True(on.IsSuccess);
        Assert.True(template.IsActive);
    }

    // ---- Delete ------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_SoftDeletes_And_Deactivates()
    {
        var template = Template();

        _templates.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);
        _templates.Setup(r => r.UpdateAsync(template, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _templates.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(template.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(template.IsDeleted);
        Assert.NotNull(template.DeletedAt);

        // Deactivated as well as deleted -- otherwise a restored row silently reappears in pickers.
        Assert.False(template.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_Refuses_A_Second_Delete()
    {
        var template = Template();
        template.IsDeleted = true;
        template.DeletedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var originalDeletedAt = template.DeletedAt;

        _templates.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        var result = await CreateSut().DeleteAsync(template.Id, ActorId);

        // Re-deleting would overwrite the original deletion timestamp and lose when it happened.
        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TEMPLATE.ALREADY_DELETED", result.Error!.Code);
        Assert.Equal(originalDeletedAt, template.DeletedAt);
    }

    [Fact]
    public async Task DeleteAsync_Returns_NotFound_For_A_Missing_Template()
    {
        var id = Guid.CreateVersion7();
        _templates.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((TicketTemplate?)null);

        var result = await CreateSut().DeleteAsync(id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TEMPLATE.NOT_FOUND", result.Error!.Code);
    }

    // ---- Read --------------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_Defaults_To_Active_Only()
    {
        _templates.Setup(r => r.GetAllAsync(EntityId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Template() });

        var result = await CreateSut().GetAllAsync(EntityId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        _templates.Verify(r => r.GetAllAsync(EntityId, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_Can_Include_Inactive_Templates()
    {
        _templates.Setup(r => r.GetAllAsync(EntityId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Template("Active"), Template("Retired", isActive: false) });

        var result = await CreateSut().GetAllAsync(EntityId, includeInactive: true);

        // Without this the admin screen could switch a template off and never switch it back on --
        // the list it reads from filtered inactive rows out unconditionally.
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, t => !t.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Template()
    {
        var id = Guid.CreateVersion7();
        _templates.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((TicketTemplate?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("TICKET_TEMPLATE.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task GetByIdAsync_Maps_The_Defaults()
    {
        var template = Template();
        template.TitleTemplate = "Password reset for {username}";
        template.SortOrder = 9;

        _templates.Setup(r => r.GetByIdAsync(template.Id, It.IsAny<CancellationToken>())).ReturnsAsync(template);

        var result = await CreateSut().GetByIdAsync(template.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Password reset for {username}", result.Value.TitleTemplate);
        Assert.Equal(TicketType.Request, result.Value.DefaultType);
        Assert.Equal(9, result.Value.SortOrder);
    }
}
