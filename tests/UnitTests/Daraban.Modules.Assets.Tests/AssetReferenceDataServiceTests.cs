using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services;
using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Assets.Tests;

/// <summary>
/// The four reference-data services (AssetType, AssetCategory, Location, Manufacturer). They
/// share a shape, so they share a file — what differs between them is which foreign key or
/// uniqueness rule each one actually enforces, and that is what these tests pin.
/// </summary>
public class AssetReferenceDataServiceTests
{
    // ---- AssetTypeService --------------------------------------------------------------------

    public class AssetTypeServiceTests
    {
        private readonly Mock<IAssetTypeRepository> _types = new(MockBehavior.Strict);
        private readonly Mock<IAssetCategoryRepository> _categories = new(MockBehavior.Strict);

        private AssetTypeService CreateSut() => new(_types.Object, _categories.Object);

        private static AssetCategory Hardware(Guid? id = null) => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            Name = "Hardware",
        };

        [Fact]
        public async Task CreateAsync_Rejects_An_Unknown_Category()
        {
            var categoryId = Guid.CreateVersion7();
            _categories.Setup(r => r.GetByIdAsync(categoryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetCategory?)null);

            var result = await CreateSut().CreateAsync(new CreateAssetTypeRequest(categoryId, "Laptop", null, null));

            // CategoryId is a required FK -- letting it through would fail at the database with a
            // constraint violation instead of a usable error.
            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.CATEGORY_NOT_FOUND", result.Error!.Code);
            _types.Verify(r => r.AddAsync(It.IsAny<AssetType>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsync_Returns_The_Resolved_Category_Name()
        {
            var category = Hardware();
            _categories.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
            _types.Setup(r => r.AddAsync(It.IsAny<AssetType>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _types.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().CreateAsync(new CreateAssetTypeRequest(category.Id, "Laptop", "Portable", "laptop"));

            Assert.True(result.IsSuccess);
            Assert.Equal("Hardware", result.Value.CategoryName);
            Assert.Equal("Laptop", result.Value.Name);
        }

        [Fact]
        public async Task UpdateAsync_Validates_Both_The_Type_And_The_New_Category()
        {
            var type = new AssetType { Id = Guid.CreateVersion7(), Name = "Laptop", CategoryId = Guid.CreateVersion7() };
            var newCategoryId = Guid.CreateVersion7();

            _types.Setup(r => r.GetByIdWithFieldsAsync(type.Id, It.IsAny<CancellationToken>())).ReturnsAsync(type);
            _categories.Setup(r => r.GetByIdAsync(newCategoryId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((AssetCategory?)null);

            var result = await CreateSut().UpdateAsync(type.Id, new CreateAssetTypeRequest(newCategoryId, "Laptop", null, null));

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.CATEGORY_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task UpdateAsync_Returns_NotFound_For_A_Missing_Type()
        {
            var id = Guid.CreateVersion7();
            _types.Setup(r => r.GetByIdWithFieldsAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((AssetType?)null);

            var result = await CreateSut().UpdateAsync(id, new CreateAssetTypeRequest(Guid.CreateVersion7(), "X", null, null));

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.ASSET_TYPE_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletes()
        {
            var type = new AssetType { Id = Guid.CreateVersion7(), Name = "Laptop", CategoryId = Guid.CreateVersion7() };

            _types.Setup(r => r.GetByIdWithFieldsAsync(type.Id, It.IsAny<CancellationToken>())).ReturnsAsync(type);
            _types.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().DeleteAsync(type.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(type.DeletedAt);
        }

        [Fact]
        public async Task GetAllAsync_Falls_Back_To_An_Empty_Category_Name_When_Not_Included()
        {
            // GetAllAsync does not eager-load Category, so the projection must tolerate a null
            // navigation rather than throwing.
            _types.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new AssetType { Id = Guid.CreateVersion7(), Name = "Laptop", CategoryId = Guid.CreateVersion7() },
                });

            var result = await CreateSut().GetAllAsync();

            Assert.True(result.IsSuccess);
            Assert.Equal(string.Empty, result.Value[0].CategoryName);
        }
    }

    // ---- AssetCategoryService ----------------------------------------------------------------

    public class AssetCategoryServiceTests
    {
        private readonly Mock<IAssetCategoryRepository> _repo = new(MockBehavior.Strict);

        private AssetCategoryService CreateSut() => new(_repo.Object);

        private static AssetCategory Category(Guid? id = null, Guid? parentId = null) => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            ParentId = parentId,
            Name = "Hardware",
        };

        [Fact]
        public async Task CreateAsync_Rejects_A_Missing_Parent()
        {
            var parentId = Guid.CreateVersion7();
            _repo.Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>())).ReturnsAsync((AssetCategory?)null);

            var result = await CreateSut().CreateAsync(new CreateAssetCategoryRequest(parentId, "Laptops", null));

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.CATEGORY_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task CreateAsync_Accepts_A_Root_Category()
        {
            _repo.Setup(r => r.AddAsync(It.IsAny<AssetCategory>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().CreateAsync(new CreateAssetCategoryRequest(null, "Hardware", "Physical kit", 5));

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.ParentId);
            Assert.Equal(5, result.Value.SortOrder);

            // No parent means no lookup at all.
            _repo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_Skips_The_Parent_Lookup_When_Reparenting_To_Itself()
        {
            var category = Category();

            _repo.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().UpdateAsync(category.Id, new CreateAssetCategoryRequest(category.Id, "Hardware", null));

            // KNOWN GAP, pinned deliberately: the `request.ParentId != id` guard skips validation
            // for self-parenting rather than rejecting it, so this succeeds and writes a
            // self-referencing row. Contrast KbCategoryService, which returns
            // KNOWLEDGE.CATEGORY_CYCLE. If AssetCategoryService is hardened later, this test
            // should be inverted.
            Assert.True(result.IsSuccess);
            Assert.Equal(category.Id, category.ParentId);
        }

        [Fact]
        public async Task UpdateAsync_Rejects_A_Missing_New_Parent()
        {
            var category = Category();
            var newParentId = Guid.CreateVersion7();

            _repo.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
            _repo.Setup(r => r.GetByIdAsync(newParentId, It.IsAny<CancellationToken>())).ReturnsAsync((AssetCategory?)null);

            var result = await CreateSut().UpdateAsync(category.Id, new CreateAssetCategoryRequest(newParentId, "Hardware", null));

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.CATEGORY_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletes_Without_Checking_For_Children()
        {
            var category = Category();

            _repo.Setup(r => r.GetByIdAsync(category.Id, It.IsAny<CancellationToken>())).ReturnsAsync(category);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().DeleteAsync(category.Id);

            // KNOWN GAP, pinned deliberately: unlike KbCategoryService, this does not refuse when
            // child categories or asset types still reference the row. The FK is Restrict at the
            // database level, but this is a soft delete, so the children silently keep pointing at
            // a deleted parent.
            Assert.True(result.IsSuccess);
            Assert.NotNull(category.DeletedAt);
        }
    }

    // ---- LocationService ---------------------------------------------------------------------

    public class LocationServiceTests
    {
        private readonly Mock<ILocationRepository> _repo = new(MockBehavior.Strict);

        private LocationService CreateSut() => new(_repo.Object);

        private static Location Building(Guid? id = null) => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            Name = "HQ",
            City = "Stockholm",
            Country = "SE",
        };

        [Fact]
        public async Task CreateAsync_Rejects_A_Missing_Parent()
        {
            var parentId = Guid.CreateVersion7();
            _repo.Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>())).ReturnsAsync((Location?)null);

            var result = await CreateSut().CreateAsync(
                new CreateLocationRequest(parentId, "Room 101", null, null, null, null));

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.LOCATION_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task CreateAsync_Accepts_A_Root_Location()
        {
            _repo.Setup(r => r.AddAsync(It.IsAny<Location>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().CreateAsync(
                new CreateLocationRequest(null, "HQ", "1 Main St", "11122", "Stockholm", "SE"));

            Assert.True(result.IsSuccess);
            Assert.Equal("HQ", result.Value.Name);
            Assert.Equal("Stockholm", result.Value.City);
        }

        [Fact]
        public async Task UpdateAsync_Returns_NotFound_For_A_Missing_Location()
        {
            var id = Guid.CreateVersion7();
            _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Location?)null);

            var result = await CreateSut().UpdateAsync(id, new CreateLocationRequest(null, "X", null, null, null, null));

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.LOCATION_NOT_FOUND", result.Error!.Code);
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletes()
        {
            var location = Building();

            _repo.Setup(r => r.GetByIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(location);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().DeleteAsync(location.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(location.DeletedAt);
        }
    }

    // ---- ManufacturerService -----------------------------------------------------------------

    public class ManufacturerServiceTests
    {
        private readonly Mock<IManufacturerRepository> _repo = new(MockBehavior.Strict);

        private ManufacturerService CreateSut() => new(_repo.Object);

        private static Manufacturer Dell(Guid? id = null) => new()
        {
            Id = id ?? Guid.CreateVersion7(),
            Name = "Dell",
        };

        [Fact]
        public async Task CreateAsync_Rejects_A_Duplicate_Name()
        {
            _repo.Setup(r => r.NameExistsAsync("Dell", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await CreateSut().CreateAsync(new CreateManufacturerRequest("Dell", null, null, null));

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.MANUFACTURER_NAME_DUPLICATE", result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
        }

        [Fact]
        public async Task CreateAsync_Persists_The_Support_Contact_Details()
        {
            _repo.Setup(r => r.NameExistsAsync("Dell", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _repo.Setup(r => r.AddAsync(It.IsAny<Manufacturer>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().CreateAsync(
                new CreateManufacturerRequest("Dell", "https://dell.com", "https://dell.com/support", "+1-800"));

            Assert.True(result.IsSuccess);
            Assert.Equal("https://dell.com/support", result.Value.SupportUrl);
            Assert.Equal("+1-800", result.Value.SupportPhone);
        }

        [Fact]
        public async Task UpdateAsync_Excludes_Itself_From_The_Name_Uniqueness_Check()
        {
            var manufacturer = Dell();

            _repo.Setup(r => r.GetByIdAsync(manufacturer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(manufacturer);
            _repo.Setup(r => r.NameExistsAsync("Dell", manufacturer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().UpdateAsync(
                manufacturer.Id, new CreateManufacturerRequest("Dell", "https://dell.com", null, null));

            // Saving without renaming must not collide with the row's own name.
            Assert.True(result.IsSuccess);
            _repo.Verify(r => r.NameExistsAsync("Dell", manufacturer.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_Rejects_Renaming_Onto_Another_Manufacturer()
        {
            var manufacturer = Dell();

            _repo.Setup(r => r.GetByIdAsync(manufacturer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(manufacturer);
            _repo.Setup(r => r.NameExistsAsync("HP", manufacturer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var result = await CreateSut().UpdateAsync(
                manufacturer.Id, new CreateManufacturerRequest("HP", null, null, null));

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.MANUFACTURER_NAME_DUPLICATE", result.Error!.Code);
        }

        [Fact]
        public async Task DeleteAsync_SoftDeletes()
        {
            var manufacturer = Dell();

            _repo.Setup(r => r.GetByIdAsync(manufacturer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(manufacturer);
            _repo.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var result = await CreateSut().DeleteAsync(manufacturer.Id);

            Assert.True(result.IsSuccess);
            Assert.NotNull(manufacturer.DeletedAt);
        }

        [Fact]
        public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Manufacturer()
        {
            var id = Guid.CreateVersion7();
            _repo.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Manufacturer?)null);

            var result = await CreateSut().GetByIdAsync(id);

            Assert.False(result.IsSuccess);
            Assert.Equal("ASSETS.MANUFACTURER_NOT_FOUND", result.Error!.Code);
        }
    }
}
