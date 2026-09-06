using Daraban.Modules.Financial.Data.Entities;
using Daraban.Modules.Financial.Data.Repositories;
using Daraban.Modules.Financial.Services;
using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Financial.Tests;

/// <summary>
/// SupplierService: plain CRUD with per-tenant name uniqueness. The delete path also deactivates
/// the row, because fleet code filters supplier pickers on IsActive -- a soft-deleted-but-active
/// supplier would keep showing up in dropdowns.
/// </summary>
public class SupplierServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<ISupplierRepository> _suppliers = new(MockBehavior.Strict);

    private SupplierService CreateSut() => new(_suppliers.Object);

    private static Supplier SupplierWith(string name = "Dell") => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = EntityId,
        Name = name,
        Type = SupplierType.Hardware,
        IsActive = true,
    };

    private static CreateSupplierRequest CreateRequest(string name = "Dell")
        => new(EntityId, name, null, "Jane Smith", "jane@dell.example", "+1-555-0100", null, null,
            "https://dell.example", null, null, null, null, null, null, null, null, null, null,
            null, SupplierType.Hardware, null);

    private static UpdateSupplierRequest UpdateRequest(string name = "Dell")
        => new(name, null, "Jane Smith", "jane@dell.example", "+1-555-0100", null, null,
            null, null, null, null, null, null, null, null, null, null, null, null,
            SupplierType.Hardware, null, true);

    /// <summary>Happy-path arrange for a single-supplier mutation.</summary>
    private void ArrangeMutation(Supplier supplier)
    {
        _suppliers.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        _suppliers.Setup(r => r.UpdateAsync(supplier, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _suppliers.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Name_With_A_Conflict()
    {
        _suppliers.Setup(r => r.NameExistsAsync("Dell", EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUPPLIER.NAME_EXISTS", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _suppliers.Verify(r => r.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Persists_The_Contact_Details_And_Starts_Active()
    {
        Supplier? captured = null;
        _suppliers.Setup(r => r.NameExistsAsync(It.IsAny<string>(), EntityId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _suppliers.Setup(r => r.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()))
            .Callback<Supplier, CancellationToken>((s, _) => captured = s)
            .Returns(Task.CompletedTask);
        _suppliers.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest(), ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(captured!.IsActive);
        Assert.Equal(7, captured.Id.Version); // UUIDv7, not v4
        Assert.Equal("jane@dell.example", captured.Email);
        Assert.Equal("+1-555-0100", captured.Phone);
        Assert.Equal(SupplierType.Hardware, captured.Type);
        Assert.Equal(ActorId, captured.CreatedById);
    }

    // ---- Update ------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Excludes_Itself_From_The_Name_Check()
    {
        var supplier = SupplierWith();
        _suppliers.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        _suppliers.Setup(r => r.NameExistsAsync("Dell", EntityId, supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _suppliers.Setup(r => r.UpdateAsync(supplier, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _suppliers.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(supplier.Id, UpdateRequest(), ActorId);

        // Saving without renaming must not collide with the row's own name.
        Assert.True(result.IsSuccess);
        _suppliers.Verify(r => r.NameExistsAsync("Dell", EntityId, supplier.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Renaming_Onto_Another_Supplier()
    {
        var supplier = SupplierWith();
        _suppliers.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        _suppliers.Setup(r => r.NameExistsAsync("HP", EntityId, supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().UpdateAsync(supplier.Id, UpdateRequest("HP"), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUPPLIER.NAME_EXISTS", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Returns_NotFound_For_A_Missing_Supplier()
    {
        var id = Guid.CreateVersion7();
        _suppliers.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Supplier?)null);

        var result = await CreateSut().UpdateAsync(id, UpdateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUPPLIER.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Can_Deactivate_A_Supplier()
    {
        var supplier = SupplierWith();
        var request = UpdateRequest();
        request = request with { IsActive = false };

        _suppliers.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);
        _suppliers.Setup(r => r.NameExistsAsync("Dell", EntityId, supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _suppliers.Setup(r => r.UpdateAsync(supplier, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _suppliers.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UpdateAsync(supplier.Id, request, ActorId);

        Assert.True(result.IsSuccess);
        Assert.False(supplier.IsActive);
    }

    // ---- Delete ------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_SoftDeletes_And_Deactivates()
    {
        var supplier = SupplierWith();
        ArrangeMutation(supplier);

        var result = await CreateSut().DeleteAsync(supplier.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(supplier.IsDeleted);
        Assert.NotNull(supplier.DeletedAt);

        // Deactivated as well as deleted -- picker queries filter on IsActive, so a deleted-but
        // -active row would keep appearing in dropdowns while being unselectable in detail views.
        Assert.False(supplier.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_Returns_NotFound_For_A_Missing_Supplier()
    {
        var id = Guid.CreateVersion7();
        _suppliers.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Supplier?)null);

        var result = await CreateSut().DeleteAsync(id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUPPLIER.NOT_FOUND", result.Error!.Code);
    }

    // ---- Read --------------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Supplier()
    {
        var id = Guid.CreateVersion7();
        _suppliers.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("SUPPLIER.NOT_FOUND", result.Error!.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task GetPagedAsync_Passes_Every_Filter_Through()
    {
        _suppliers.Setup(r => r.GetPagedAsync(
                EntityId, "dell", SupplierType.Hardware, true, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Supplier>(), 0));

        var result = await CreateSut().GetPagedAsync(EntityId, "dell", SupplierType.Hardware, true, 1, 20);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalCount);
    }
}