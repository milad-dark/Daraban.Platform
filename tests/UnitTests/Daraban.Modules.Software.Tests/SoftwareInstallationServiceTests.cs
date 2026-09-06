using Daraban.Modules.Software.Data.Entities;
using Daraban.Modules.Software.Data.Repositories;
using Daraban.Modules.Software.Services;
using Daraban.Modules.Software.Services.Dtos;
using Daraban.Platform.Common;
using Moq;
using Xunit;

namespace Daraban.Modules.Software.Tests;

/// <summary>
/// SoftwareInstallationService: recording what is actually deployed where. The compliance
/// guard on creation and the seat-counter bookkeeping are the load-bearing parts -- the license
/// pool must never go negative and must never silently allow an install it cannot cover.
/// </summary>
public class SoftwareInstallationServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<ISoftwareInstallationRepository> _installations = new(MockBehavior.Strict);
    private readonly Mock<ISoftwareRepository> _software = new(MockBehavior.Strict);
    private readonly Mock<ISoftwareLicenseRepository> _licenses = new(MockBehavior.Strict);

    private SoftwareInstallationService CreateSut() =>
        new(_installations.Object, _software.Object, _licenses.Object);

    private static SoftwareProduct Product(Guid? entityId = null) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = entityId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Name = "Microsoft Office",
        Category = SoftwareCategory.OfficeSuite,
    };

    private static SoftwareLicense LicenseFor(
        Guid softwareId, Guid entityId,
        int quantity = 10, int used = 0,
        bool isActive = true, DateTimeOffset? expires = null) => new()
    {
        Id = Guid.CreateVersion7(),
        EntityId = entityId,
        SoftwareId = softwareId,
        Name = "Office 2024 Volume",
        Type = LicenseType.Volume,
        Quantity = quantity,
        UsedQuantity = used,
        ExpirationDate = expires,
        IsActive = isActive,
    };

    private static SoftwareInstallation Installation(
        Guid softwareId, Guid? licenseId = null, Guid? assetId = null, bool isActive = true) => new()
    {
        Id = Guid.CreateVersion7(),
        SoftwareId = softwareId,
        LicenseId = licenseId,
        AssetId = assetId ?? Guid.CreateVersion7(),
        InstalledVersion = "16.0",
        InstalledDate = DateTimeOffset.UtcNow,
        IsActive = isActive,
    };

    private static CreateSoftwareInstallationRequest Request(
        Guid softwareId, Guid? licenseId, Guid assetId)
        => new(softwareId, licenseId, assetId, "16.0", @"C:\Program Files\Office", InstallationSource.Agent, null);

    // ---- Create: guards ----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Rejects_An_Unknown_Product()
    {
        var softwareId = Guid.CreateVersion7();
        _software.Setup(r => r.GetByIdAsync(softwareId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoftwareProduct?)null);

        var result = await CreateSut().CreateAsync(
            Request(softwareId, null, Guid.CreateVersion7()), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SOFTWARE.NOT_FOUND", result.Error!.Code);
        _installations.Verify(r => r.AddAsync(It.IsAny<SoftwareInstallation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_An_Unknown_License()
    {
        var product = Product();
        var licenseId = Guid.CreateVersion7();
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _licenses.Setup(r => r.GetByIdAsync(licenseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoftwareLicense?)null);

        var result = await CreateSut().CreateAsync(Request(product.Id, licenseId, Guid.CreateVersion7()), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_License_For_A_Different_Product()
    {
        var product = Product();
        var license = LicenseFor(Guid.CreateVersion7(), product.EntityId);
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);

        var result = await CreateSut().CreateAsync(Request(product.Id, license.Id, Guid.CreateVersion7()), ActorId);

        // Without this, a seat from an unrelated pool is consumed and both products' compliance
        // counts lie.
        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.SOFTWARE_MISMATCH", result.Error!.Code);
        Assert.Equal(ErrorType.BusinessRule, result.Error.Type);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_License_From_Another_Tenant()
    {
        var product = Product();
        var license = LicenseFor(product.Id, Guid.CreateVersion7());
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);

        var result = await CreateSut().CreateAsync(Request(product.Id, license.Id, Guid.CreateVersion7()), ActorId);

        // Cross-tenant seat consumption through a valid-looking call.
        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.CROSS_ENTITY", result.Error!.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
    }

    [Fact]
    public async Task CreateAsync_Rejects_An_Inactive_License()
    {
        var product = Product();
        var license = LicenseFor(product.Id, product.EntityId, isActive: false);
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);

        var result = await CreateSut().CreateAsync(Request(product.Id, license.Id, Guid.CreateVersion7()), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.INACTIVE", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Rejects_An_Expired_License()
    {
        var product = Product();
        var license = LicenseFor(product.Id, product.EntityId, expires: DateTimeOffset.UtcNow.AddDays(-1));
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);

        var result = await CreateSut().CreateAsync(Request(product.Id, license.Id, Guid.CreateVersion7()), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.EXPIRED", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Rejects_An_Install_When_No_Seats_Remain()
    {
        var product = Product();
        var license = LicenseFor(product.Id, product.EntityId, quantity: 2, used: 2);
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await CreateSut().CreateAsync(Request(product.Id, license.Id, Guid.CreateVersion7()), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("LICENSE.COMPLIANCE", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_Rejects_A_Duplicate_Install_On_The_Same_Asset()
    {
        var product = Product();
        var assetId = Guid.CreateVersion7();
        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _installations.Setup(r => r.AssetHasInstallationAsync(assetId, product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().CreateAsync(Request(product.Id, null, assetId), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("INSTALLATION.ALREADY_EXISTS", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public async Task CreateAsync_Allows_An_Unlicensed_Install()
    {
        var product = Product();
        var assetId = Guid.CreateVersion7();
        SoftwareInstallation? captured = null;

        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _installations.Setup(r => r.AssetHasInstallationAsync(assetId, product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _installations.Setup(r => r.AddAsync(It.IsAny<SoftwareInstallation>(), It.IsAny<CancellationToken>()))
            .Callback<SoftwareInstallation, CancellationToken>((i, _) => captured = i)
            .Returns(Task.CompletedTask);
        _installations.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _licenses.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(Request(product.Id, null, assetId), ActorId);

        // Freeware, open source, and agent-discovered software legitimately have no license row.
        Assert.True(result.IsSuccess);
        Assert.Null(captured!.LicenseId);
        Assert.True(captured.IsActive);
        Assert.Equal(assetId, captured.AssetId);
        Assert.Equal(7, captured.Id.Version);

        // No license touched at all.
        _licenses.Verify(r => r.UpdateAsync(It.IsAny<SoftwareLicense>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Increments_The_License_Seat_Counter()
    {
        var product = Product();
        var assetId = Guid.CreateVersion7();
        var license = LicenseFor(product.Id, product.EntityId, quantity: 10, used: 4);

        _software.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        _installations.Setup(r => r.GetActiveCountByLicenseIdAsync(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        _installations.Setup(r => r.AssetHasInstallationAsync(assetId, product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _installations.Setup(r => r.AddAsync(It.IsAny<SoftwareInstallation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _licenses.Setup(r => r.UpdateAsync(license, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _installations.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _licenses.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(Request(product.Id, license.Id, assetId), ActorId);

        // UsedQuantity feeds the compliance DTOs and the entity-level IsCompliant, and nothing
        // else ever writes it -- without this increment the pool would report 10 of 10 forever.
        Assert.True(result.IsSuccess);
        Assert.Equal(5, license.UsedQuantity);
        _licenses.Verify(r => r.UpdateAsync(license, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Uninstall ----------------------------------------------------------------------------

    [Fact]
    public async Task UninstallAsync_Returns_NotFound_For_A_Missing_Installation()
    {
        var id = Guid.CreateVersion7();
        _installations.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoftwareInstallation?)null);

        var result = await CreateSut().UninstallAsync(id, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("INSTALLATION.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task UninstallAsync_Rejects_An_Already_Uninstalled_Row()
    {
        var installation = Installation(Guid.CreateVersion7(), isActive: false);
        _installations.Setup(r => r.GetByIdAsync(installation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installation);

        var result = await CreateSut().UninstallAsync(installation.Id, ActorId);

        // Without this, a second uninstall would re-stamp UninstalledDate, rewriting history.
        Assert.False(result.IsSuccess);
        Assert.Equal("INSTALLATION.ALREADY_UNINSTALLED", result.Error!.Code);
    }

    [Fact]
    public async Task UninstallAsync_Deactivates_And_Returns_The_Seat()
    {
        var license = LicenseFor(Guid.CreateVersion7(), Guid.CreateVersion7(), quantity: 10, used: 6);
        var installation = Installation(license.SoftwareId, license.Id, isActive: true);

        _installations.Setup(r => r.GetByIdAsync(installation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installation);
        _installations.Setup(r => r.UpdateAsync(installation, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        _licenses.Setup(r => r.UpdateAsync(license, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _installations.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _licenses.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UninstallAsync(installation.Id, ActorId);

        Assert.True(result.IsSuccess);
        Assert.False(installation.IsActive);
        Assert.NotNull(installation.UninstalledDate);
        Assert.Equal(5, license.UsedQuantity);
    }

    [Fact]
    public async Task UninstallAsync_Floors_The_Seat_Counter_At_Zero()
    {
        // A counter that drifted (historical installs before the counter existed) must not go
        // negative when seats are released.
        var license = LicenseFor(Guid.CreateVersion7(), Guid.CreateVersion7(), quantity: 10, used: 0);
        var installation = Installation(license.SoftwareId, license.Id, isActive: true);

        _installations.Setup(r => r.GetByIdAsync(installation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installation);
        _installations.Setup(r => r.UpdateAsync(installation, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _licenses.Setup(r => r.GetByIdAsync(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        _licenses.Setup(r => r.UpdateAsync(license, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _installations.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _licenses.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await CreateSut().UninstallAsync(installation.Id, ActorId);

        Assert.Equal(0, license.UsedQuantity);
    }

    [Fact]
    public async Task UninstallAsync_Of_An_Unlicensed_Install_Touches_No_License()
    {
        var installation = Installation(Guid.CreateVersion7(), licenseId: null, isActive: true);

        _installations.Setup(r => r.GetByIdAsync(installation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(installation);
        _installations.Setup(r => r.UpdateAsync(installation, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _installations.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateSut().UninstallAsync(installation.Id, ActorId);

        Assert.True(result.IsSuccess);
        _licenses.Verify(r => r.UpdateAsync(It.IsAny<SoftwareLicense>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Reads -------------------------------------------------------------------------------

    [Fact]
    public async Task GetAssetSummaryAsync_Counts_Active_Licensed_And_Total()
    {
        var assetId = Guid.CreateVersion7();
        var rows = new[]
        {
            Installation(Guid.CreateVersion7(), Guid.CreateVersion7(), assetId, isActive: true),
            Installation(Guid.CreateVersion7(), licenseId: null, assetId, isActive: true),
            Installation(Guid.CreateVersion7(), Guid.CreateVersion7(), assetId, isActive: false),
        };

        _installations.Setup(r => r.GetByAssetIdAsync(assetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var result = await CreateSut().GetAssetSummaryAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(assetId, result.Value.AssetId);
        Assert.Equal(3, result.Value.TotalSoftware);
        Assert.Equal(2, result.Value.ActiveInstallations);
        Assert.Equal(2, result.Value.WithLicense);
    }

    [Fact]
    public async Task GetByIdAsync_Returns_NotFound_For_A_Missing_Installation()
    {
        var id = Guid.CreateVersion7();
        _installations.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SoftwareInstallation?)null);

        var result = await CreateSut().GetByIdAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("INSTALLATION.NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task GetByAssetIdAsync_Returns_An_Empty_List_When_Nothing_Is_Installed()
    {
        var assetId = Guid.CreateVersion7();
        _installations.Setup(r => r.GetByAssetIdAsync(assetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SoftwareInstallation>());

        var result = await CreateSut().GetByAssetIdAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
