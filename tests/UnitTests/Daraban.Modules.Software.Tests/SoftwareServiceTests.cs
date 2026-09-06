using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Data.Repositories;
using Daraban.Modules.Software.Services;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Software.Tests;

/// <summary>
/// SoftwareService: per-tenant name uniqueness on the catalog product itself. Straightforward
/// CRUD, but the names are what every license and installation resolves against, so a duplicate
/// here poisons lookups downstream.
/// </summary>
public class SoftwareServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<ISoftwareRepository> _software = new(MockBehavior.Strict);

    private SoftwareService CreateSut() => new(_software.Object);

    private static SoftwareProduct Product(string name = "Microsoft Office") => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = EntityId,
        Name = name,
        Category = SoftwareCategory.OfficeSuite,
        IsActive = true,
    };

    private static CreateSoftwareRequest CreateRequest(string name = "Microsoft Office")
        => new(EntityId, name, "2024", "Microsoft", null, SoftwareCategory.OfficeSuite,
            "Pro", false, false, null, null, null);

    private static UpdateSoftwareRequest UpdateRequest(string name = "Microsoft Office")
        => new(name, "2024", "Microsoft", null, SoftwareCategory.OfficeSuite,
            "Pro", true, false, false, null, null, null);

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Name_With_A_Conflict()
    {
        _software.Setup(r => r.NameExistsAsync("Microsoft Office", EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SOFTWARE.NAME_EXISTS", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _software.Verify(r => r.AddAsync(It.IsAny<SoftwareProduct>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Persists_An_Active_Product_With_A_Uuidv7_Id()
    {
        SoftwareProduct? captured = null;
        _software.Setup(r => r.NameExistsAsync(It.IsAny<string>(), EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _software.Setup(r => r.AddAsync(It.IsAny<SoftwareProduct>(), It.IsAny<CancellationToken>()))
            .Callback<SoftwareProduct, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);
        _software.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(
            CreateRequest() with { IsOpenSource = true, IsFree = true }, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(captured!.IsActive);
        Assert.True(captured.IsOpenSource);
        Assert.True(captured.IsFree);
        Assert.Equal(EntityId, captured.EntityId);
        Assert.Equal(7, captured.Id.Version);
    }

    [Fact]
    public async Task UpdateAsync_Excludes_Itself_From_The_Name_Check()
    {
        var product = Product();
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _software.Setup(r => r.NameExistsAsync("Microsoft Office", EntityId, product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _software.Setup(r => r.UpdateAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _software.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(product.Id, UpdateRequest(), ActorId);

        // Saving without renaming must not collide with the row's own name.
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Renaming_Onto_Another_Product()
    {
        var product = Product();
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _software.Setup(r => r.NameExistsAsync("LibreOffice", EntityId, product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().UpdateAsync(product.Id, UpdateRequest("LibreOffice"), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SOFTWARE.NAME_EXISTS", result.Error!.Code);
        Assert.Equal("Microsoft Office", product.Name);
    }

    [Fact]
    public async Task UpdateAsync_Can_Deactivate_A_Product()
    {
        var product = Product();
        var request = UpdateRequest();
        request = request with { IsActive = false };

        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _software.Setup(r => r.NameExistsAsync(It.IsAny<string>(), EntityId, product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _software.Setup(r => r.UpdateAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _software.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(product.Id, request, ActorId);

        Assert.True(result.IsSuccess);
        Assert.False(product.IsActive);
        Assert.Equal(ActorId, product.UpdatedById);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        var product = Product();
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _software.Setup(r => r.UpdateAsync(product, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _software.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(product.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(product.IsDeleted);
        Assert.NotNull(product.DeletedAt);
    }

    [Theory]
    [InlineData(0, 20, 1, 20)]
    [InlineData(3, 0, 3, 20)]
    [InlineData(1, 5000, 1, 200)]
    public async Task GetPagedAsync_Normalizes_Paging(int page, int pageSize, int expPage, int expSize)
    {
        _software.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, expPage, expSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<SoftwareProduct>(), 0));

        var result = await CreateSut().GetPagedAsync(EntityId, null, null, null, page, pageSize);

        Assert.True(result.IsSuccess);
        Assert.Equal(expPage, result.Value.Page);
        Assert.Equal(expSize, result.Value.PageSize);
    }

    [Fact]
    public async Task GetPagedAsync_Passes_The_Category_Filter_Through()
    {
        _software.Setup(r => r.GetPagedAsync(
                EntityId, null, SoftwareCategory.Antivirus, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<SoftwareProduct>(), 0));

        var result = await CreateSut().GetPagedAsync(EntityId, null, SoftwareCategory.Antivirus, null, 1, 20);

        Assert.True(result.IsSuccess);
        _software.Verify(r => r.GetPagedAsync(
            EntityId, null, SoftwareCategory.Antivirus, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Product()
    {
        var id = Guid.CreateVersion7();
        _software.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoftwareProduct?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("SOFTWARE.NOT_FOUND", result.Error!.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
