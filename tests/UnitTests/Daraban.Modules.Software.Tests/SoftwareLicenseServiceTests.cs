using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Data.Repositories;
using Daraban.Modules.Software.Services;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Software.Tests;

/// <summary>
/// SoftwareLicenseService: seat accounting. A license is a finite pool of seats, and every
/// mutation here is judged by whether that pool stays truthful -- creation validates the floor,
/// updates cannot shrink below consumption, and deletion is refused while seats are checked out.
/// </summary>
public class SoftwareLicenseServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<ISoftwareLicenseRepository> _licenses = new(MockBehavior.Strict);
    private readonly Mock<ISoftwareInstallationRepository> _installations = new(MockBehavior.Strict);
    private readonly Mock<ISoftwareRepository> _software = new(MockBehavior.Strict);

    private SoftwareLicenseService CreateSut() =>
        new(_licenses.Object, _installations.Object, _software.Object);

    private static SoftwareProduct Product(Guid? id = null, Guid? entityId = null) => new()
    {
        Id = id ?? Guid.CreateVersion7(),
        EntityId = entityId ?? EntityId,
        Name = "Microsoft Office",
        Category = SoftwareCategory.OfficeSuite,
    };

    private static SoftwareLicense LicenseWith(
        int quantity = 10, int used = 0, bool isActive = true,
        DateTimeOffset? expires = null, Guid? softwareId = null) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = EntityId,
        SoftwareId = softwareId ?? Guid.CreateVersion7(),
        Name = "Office 2024 Volume",
        Type = LicenseType.Volume,
        Quantity = quantity,
        UsedQuantity = used,
        ExpirationDate = expires,
        IsActive = isActive,
    };

    private static CreateSoftwareLicenseRequest CreateRequest(
        Guid softwareId, int quantity = 10, DateTimeOffset? expires = null)
        => new(EntityId, softwareId, "Office 2024 Volume", "XXXX-XXXX", LicenseType.Volume, quantity,
            null, expires, false, 4999, "USD", null, null, null);

    private static UpdateSoftwareLicenseRequest UpdateRequest(int quantity = 10, bool isActive = true)
        => new("Office 2024 Volume", "XXXX-XXXX", LicenseType.Volume, quantity, null, null,
            false, 4999, "USD", null, null, null, isActive);

    // ---- Create ------------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_A_License_For_A_Missing_Product()
    {
        var softwareId = Guid.CreateVersion7();
        _software.Setup(r => r.GetByIdAsync(softwareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoftwareProduct?)null);

        var result = await CreateSut().CreateAsync(CreateRequest(softwareId), ActorId);

        // A license row pointing at nothing is an orphan the catalog cannot display and
        // compliance cannot evaluate.
        Assert.False(result.IsSuccess);
        Assert.Equal("SOFTWARE.NOT_FOUND", result.Error!.Code);
        _licenses.Verify(r => r.AddAsync(It.IsAny<SoftwareLicense>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_License_In_A_Different_Tenant_Than_Its_Product()
    {
        var product = Product(entityId: Guid.CreateVersion7());
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await CreateSut().CreateAsync(CreateRequest(product.Id), ActorId);

        // Without this, an install in entity A could consume a seat from a license owned by
        // entity B -- cross-tenant seat theft through a valid-looking API call.
        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.CROSS_ENTITY", result.Error!.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateAsync_Rejects_A_NonPositive_Seat_Count(int quantity)
    {
        var product = Product();
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await CreateSut().CreateAsync(CreateRequest(product.Id, quantity), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.INVALID_QUANTITY", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Persists_An_Active_License_With_Zero_Usage()
    {
        var product = Product();
        SoftwareLicense? captured = null;

        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _licenses.Setup(r => r.AddAsync(It.IsAny<SoftwareLicense>(), It.IsAny<CancellationToken>()))
            .Callback<SoftwareLicense, CancellationToken>((l, _) => captured = l)
            .Returns(Task.CompletedTask);
        _licenses.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(CreateRequest(product.Id), ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(captured!.IsActive);
        Assert.Equal(0, captured.UsedQuantity);
        Assert.Equal(10, captured.AvailableQuantity);
        Assert.True(captured.IsCompliant);
        Assert.Equal(7, captured.Id.Version); // UUIDv7, not v4
    }

    // ---- Update ------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Rejects_A_NonPositive_Seat_Count()
    {
        var license = LicenseWith();
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);

        var result = await CreateSut().UpdateAsync(license.Id, UpdateRequest(quantity: 0), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.INVALID_QUANTITY", result.Error!.Code);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Shrinking_Below_Seats_Already_In_Use()
    {
        var license = LicenseWith(quantity: 10, used: 7);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var result = await CreateSut().UpdateAsync(license.Id, UpdateRequest(quantity: 5), ActorId);

        // Cutting the pool below live consumption would flip a compliant fleet non-compliant
        // retroactively, with no record of the change.
        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.QUANTITY_BELOW_USAGE", result.Error!.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
        Assert.Equal(10, license.Quantity);
    }

    [Fact]
    public async Task UpdateAsync_Allows_Growing_And_Shrinking_Above_Usage()
    {
        var license = LicenseWith(quantity: 10, used: 7);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        _licenses.Setup(r => r.UpdateAsync(license, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _licenses.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Down to exactly the usage floor is legal; the check is strict less-than.
        var result = await CreateSut().UpdateAsync(license.Id, UpdateRequest(quantity: 7), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, license.Quantity);
    }

    [Fact]
    public async Task UpdateAsync_Returns_NotFound_For_A_Missing_License()
    {
        var id = Guid.CreateVersion7();
        _licenses.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SoftwareLicense?)null);

        var result = await CreateSut().UpdateAsync(id, UpdateRequest(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.NOT_FOUND", result.Error!.Code);
    }

    // ---- Delete ------------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Refuses_A_License_With_Active_Installations()
    {
        var license = LicenseWith(used: 3);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await CreateSut().DeleteAsync(license.Id, ActorId);

        // Deleting the pool while seats are checked out strands those installations with a
        // dangling license reference.
        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.IN_USE", result.Error!.Code);
        Assert.False(license.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_A_License_With_No_Active_Installations()
    {
        var license = LicenseWith();
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _licenses.Setup(r => r.UpdateAsync(license, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _licenses.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().DeleteAsync(license.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(license.IsDeleted);
        Assert.NotNull(license.DeletedAt);
    }

    [Fact]
    public async Task DeleteAsync_Returns_NotFound_For_A_Missing_License()
    {
        var id = Guid.CreateVersion7();
        _licenses.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((SoftwareLicense?)null);

        var result = await CreateSut().DeleteAsync(id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.NOT_FOUND", result.Error!.Code);
    }

    // ---- Compliance --------------------------------------------------------------------------

    [Fact]
    public async Task CheckComplianceAsync_Reports_Compliant_When_Seats_Remain()
    {
        var license = LicenseWith(quantity: 10, used: 7);
        _licenses.Setup(r => r.GetByIdWithDetailsAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var result = await CreateSut().CheckComplianceAsync(license.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsCompliant);
        Assert.False(result.Value.IsExpired);
        Assert.Equal(7, result.Value.InstalledCount);
        Assert.Equal(3, result.Value.AvailableCount);
    }

    [Fact]
    public async Task CheckComplianceAsync_Reports_NonCompliant_When_OverAllocated()
    {
        var license = LicenseWith(quantity: 2, used: 2);
        _licenses.Setup(r => r.GetByIdWithDetailsAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // one seat too many -- e.g. added by the agent path historically

        var result = await CreateSut().CheckComplianceAsync(license.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsCompliant);
        Assert.Equal(-1, result.Value.AvailableCount);
    }

    [Fact]
    public async Task CheckComplianceAsync_Reports_Expired_And_NonCompliant()
    {
        var license = LicenseWith(quantity: 10, used: 2, expires: DateTimeOffset.UtcNow.AddDays(-1));
        _licenses.Setup(r => r.GetByIdWithDetailsAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await CreateSut().CheckComplianceAsync(license.Id);

        // Seats remaining do not matter once the term has lapsed: the fleet is unlicensed.
        Assert.True(result.Value.IsExpired);
        Assert.False(result.Value.IsCompliant);
    }

    [Fact]
    public async Task CheckComplianceAsync_Returns_NotFound_For_A_Missing_License()
    {
        var id = Guid.CreateVersion7();
        _licenses.Setup(r => r.GetByIdWithDetailsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoftwareLicense?)null);

        var result = await CreateSut().CheckComplianceAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.NOT_FOUND", result.Error!.Code);
    }

    // ---- Read --------------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 20, 1, 20)]
    [InlineData(2, 0, 2, 20)]
    [InlineData(1, 5000, 1, 200)]
    public async Task GetPagedAsync_Normalizes_Paging(int page, int pageSize, int expPage, int expSize)
    {
        _licenses.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, expPage, expSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<SoftwareLicense>(), 0));

        var result = await CreateSut().GetPagedAsync(EntityId, null, null, null, page, pageSize);

        Assert.True(result.IsSuccess);
        Assert.Equal(expPage, result.Value.Page);
        Assert.Equal(expSize, result.Value.PageSize);
    }

    [Fact]
    public async Task GetPagedAsync_Passes_The_Type_Filter_Through()
    {
        _licenses.Setup(r => r.GetPagedAsync(
                EntityId, null, LicenseType.SaaS, null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<SoftwareLicense>(), 0));

        var result = await CreateSut().GetPagedAsync(EntityId, null, LicenseType.SaaS, null, 1, 20);

        Assert.True(result.IsSuccess);
        _licenses.Verify(r => r.GetPagedAsync(
            EntityId, null, LicenseType.SaaS, null, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }
}
