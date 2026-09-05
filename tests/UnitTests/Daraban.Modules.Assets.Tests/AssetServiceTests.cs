using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Assets.Tests;

/// <summary>
/// AssetService with both repositories mocked: uniqueness guards on asset tag and serial
/// number, the status the service forces on creation, and the fact that Update never lets a
/// caller change the tenant or asset type.
/// </summary>
public class AssetServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IAssetRepository> _assets = new(MockBehavior.Strict);
    private readonly Mock<IAssetTypeRepository> _assetTypes = new(MockBehavior.Strict);

    private AssetService CreateSut() => new(_assets.Object, _assetTypes.Object);

    private static AssetType Laptop(Guid? id = null) => new()
    {
        Id = id ?? Guid.CreateVersion7(),
        CategoryId = Guid.CreateVersion7(),
        Name = "Laptop",
    };

    private static Asset Existing(
        Guid typeId,
        AssetStatus status = AssetStatus.InStock,
        string? tag = "AT-1",
        string? serial = "SN-1")
        => new()
        {
            Id = Guid.CreateVersion7(),
            AssetTypeId = typeId,
            EntityNodeId = EntityId,
            Name = "Dell XPS",
            AssetTag = tag,
            SerialNumber = serial,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static CreateAssetRequest CreateRequest(
        Guid typeId, string? tag = "AT-9", string? serial = "SN-9")
        => new("Dell XPS", typeId, null, null, EntityId, tag, serial,
            null, null, null, null, null, null, null);

    private static UpdateAssetRequest UpdateRequest(string? tag = "AT-9", string? serial = "SN-9")
        => new("Renamed", null, null, tag, serial, null, null, null, null, null, null, null);

    // ---- Create -----------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_An_Unknown_AssetType()
    {
        var typeId = Guid.CreateVersion7();
        _assetTypes.Setup(r => r.GetByIdWithFieldsAsync(typeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AssetType?)null);

        var result = await CreateSut().CreateAsync(CreateRequest(typeId), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSET_TYPE_NOT_FOUND", result.Error!.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        _assets.Verify(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_AssetTag()
    {
        var type = Laptop();
        _assetTypes.Setup(r => r.GetByIdWithFieldsAsync(type.Id, It.IsAny<CancellationToken>())).ReturnsAsync(type);
        _assets.Setup(r => r.AssetTagExistsAsync("AT-9", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest(type.Id), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSET_TAG_DUPLICATE", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_SerialNumber()
    {
        var type = Laptop();
        _assetTypes.Setup(r => r.GetByIdWithFieldsAsync(type.Id, It.IsAny<CancellationToken>())).ReturnsAsync(type);
        _assets.Setup(r => r.AssetTagExistsAsync("AT-9", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SerialNumberExistsAsync("SN-9", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest(type.Id), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.SERIAL_NUMBER_DUPLICATE", result.Error!.Code);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    public async Task CreateAsync_Skips_Uniqueness_Checks_When_Tag_And_Serial_Are_Blank(
        string? tag, string? serial)
    {
        var type = Laptop();
        _assetTypes.Setup(r => r.GetByIdWithFieldsAsync(type.Id, It.IsAny<CancellationToken>())).ReturnsAsync(type);
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest(type.Id, tag, serial), ActorId);

        Assert.True(result.IsSuccess);

        // A blank tag/serial is "not set", not a value competing for uniqueness -- querying the
        // database for it would be wasted work and could produce a false conflict.
        _assets.Verify(r => r.AssetTagExistsAsync(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        _assets.Verify(r => r.SerialNumberExistsAsync(
            It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Always_Starts_The_Asset_InStock()
    {
        var type = Laptop();
        Asset? captured = null;

        _assetTypes.Setup(r => r.GetByIdWithFieldsAsync(type.Id, It.IsAny<CancellationToken>())).ReturnsAsync(type);
        _assets.Setup(r => r.AssetTagExistsAsync("AT-9", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SerialNumberExistsAsync("SN-9", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest(type.Id), ActorId);

        Assert.True(result.IsSuccess);

        // An asset becomes InUse only through AssetAssignmentService -- creation cannot skip that.
        Assert.Equal(AssetStatus.InStock, captured!.Status);
        Assert.Equal(EntityId, captured.EntityNodeId);
        Assert.NotEqual(Guid.Empty, captured.Id);
    }

    // ---- Update -----------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Returns_NotFound_For_A_Missing_Asset()
    {
        var id = Guid.CreateVersion7();
        _assets.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);

        var result = await CreateSut().UpdateAsync(id, UpdateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSET_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Excludes_The_Asset_Itself_From_The_Uniqueness_Check()
    {
        var type = Laptop();
        var asset = Existing(type.Id);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assets.Setup(r => r.AssetTagExistsAsync("AT-9", asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SerialNumberExistsAsync("SN-9", asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(asset.Id, UpdateRequest(), ActorId);

        Assert.True(result.IsSuccess);

        // Without excludeId, saving an asset without changing its tag would collide with itself.
        _assets.Verify(r => r.AssetTagExistsAsync("AT-9", asset.Id, It.IsAny<CancellationToken>()), Times.Once);
        _assets.Verify(r => r.SerialNumberExistsAsync("SN-9", asset.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Cannot_Move_An_Asset_To_Another_Tenant_Or_Type()
    {
        var type = Laptop();
        var asset = Existing(type.Id);
        var originalType = asset.AssetTypeId;
        var originalEntity = asset.EntityNodeId;

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assets.Setup(r => r.AssetTagExistsAsync("AT-9", asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SerialNumberExistsAsync("SN-9", asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateAsync(asset.Id, UpdateRequest(), ActorId);

        // UpdateAssetRequest deliberately carries neither field -- re-typing or re-homing an
        // asset is not an edit, and would invalidate its custom field values.
        Assert.Equal(originalType, asset.AssetTypeId);
        Assert.Equal(originalEntity, asset.EntityNodeId);
        Assert.Equal("Renamed", asset.Name);
    }

    [Fact]
    public async Task UpdateAsync_Does_Not_Change_Status()
    {
        var type = Laptop();
        var asset = Existing(type.Id, AssetStatus.InUse);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assets.Setup(r => r.AssetTagExistsAsync("AT-9", asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SerialNumberExistsAsync("SN-9", asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UpdateAsync(asset.Id, UpdateRequest(), ActorId);

        // Status transitions belong to AssetLifecycleService, which validates the state machine
        // and records history. A plain edit must not bypass that.
        Assert.Equal(AssetStatus.InUse, asset.Status);
    }

    // ---- Delete + read ----------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_SoftDeletes_By_Stamping_DeletedAt()
    {
        var type = Laptop();
        var asset = Existing(type.Id);

        _assets.Setup(r => r.GetByIdAsync(asset.Id, It.IsAny<CancellationToken>())).ReturnsAsync(asset);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(asset.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(asset.DeletedAt);
    }

    [Fact]
    public async Task DeleteAsync_Returns_NotFound_For_A_Missing_Asset()
    {
        var id = Guid.CreateVersion7();
        _assets.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Asset?)null);

        var result = await CreateSut().DeleteAsync(id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.ASSET_NOT_FOUND", result.Error!.Code);
    }

    [Theory]
    [InlineData("InUse", AssetStatus.InUse)]
    [InlineData("inuse", AssetStatus.InUse)]
    [InlineData("RETIRED", AssetStatus.Retired)]
    public async Task GetPagedAsync_Parses_The_Status_Filter_Case_Insensitively(
        string input, AssetStatus expected)
    {
        _assets.Setup(r => r.GetPagedAsync(
                EntityId, expected, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Asset>(), 0));

        var result = await CreateSut().GetPagedAsync(EntityId, input, null, null, null, 1, 20);

        Assert.True(result.IsSuccess);
        _assets.Verify(r => r.GetPagedAsync(
            EntityId, expected, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("NotAStatus")]
    [InlineData("")]
    [InlineData(null)]
    public async Task GetPagedAsync_Treats_An_Unparseable_Status_As_No_Filter(string? input)
    {
        _assets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Asset>(), 0));

        var result = await CreateSut().GetPagedAsync(EntityId, input, null, null, null, 1, 20);

        // Garbage in the query string returns everything rather than erroring -- but it must not
        // silently become some arbitrary status either.
        Assert.True(result.IsSuccess);
        _assets.Verify(r => r.GetPagedAsync(
            EntityId, null, null, null, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPagedAsync_Echoes_Total_And_Paging_From_The_Repository()
    {
        var type = Laptop();
        var page = new[] { Existing(type.Id), Existing(type.Id) };

        _assets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, 2, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((page, 7));

        var result = await CreateSut().GetPagedAsync(EntityId, null, null, null, null, 2, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Page);
        Assert.Equal(2, result.Value.Items.Count);
    }
}
