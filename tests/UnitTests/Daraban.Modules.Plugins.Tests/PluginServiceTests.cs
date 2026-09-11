using Daraban.Modules.Plugins.Data.Entities;
using Daraban.Modules.Plugins.Data.Repositories;
using Daraban.Modules.Plugins.Services;
using Daraban.Platform.Common;
using Daraban.Platform.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Daraban.Modules.Plugins.Tests;

/// <summary>
/// PluginService (Task 7.5): the registry state machine on top of the runtime manager.
/// The manager and repository are mocked (their ALC/DB choreography is not what's under
/// test); what matters here is that every invalid transition is refused, every refusal
/// carries a typed Error, and successful installs record the registry row.
/// </summary>
public class PluginServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IPluginRepository> _repository = new();
    private readonly Mock<IPluginManager> _manager = new();
    private readonly PluginService _sut;

    public PluginServiceTests()
    {
        _sut = new PluginService(_repository.Object, _manager.Object, NullLogger<PluginService>.Instance);
    }

    private static Plugin Row(string pluginId, string status) => new()
    {
        PluginId = pluginId,
        Name = pluginId,
        Version = "1.0.0",
        Type = PluginTypes.Asset,
        Status = status,
        InstalledAt = DateTimeOffset.UtcNow,
        ManifestJson = "{}",
    };

    private void SetupInstallSucceeds(string pluginId = "asset-importer")
    {
        _manager
            .Setup(m => m.InstallAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PluginManager.InstallOutcome.Succeeded(new PluginManifest
            {
                Id = pluginId,
                Type = PluginTypes.Asset,
                Name = "Asset Importer",
                Version = "1.0.0",
                EntryPoint = "AssetImporter.Plugin",
                AssemblyFile = "AssetImporter.dll",
            }));
    }

    // ---- Install ---------------------------------------------------------------------------------

    [Fact]
    public async Task Install_WithValidPackage_RecordsEnabledRegistryRow()
    {
        SetupInstallSucceeds();
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Plugin?)null);

        var result = await _sut.InstallAsync(new MemoryStream([1, 2, 3]), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginManager.EnabledStatus, result.Value!.Status);
        Assert.Equal(ActorId, result.Value.CreatedById);
        _repository.Verify(r => r.AddAsync(It.IsAny<Plugin>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Install_WhenManagerRejects_ReturnsValidationErrorAndPersistsNothing()
    {
        _manager
            .Setup(m => m.InstallAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PluginManager.InstallOutcome.Failed(["Package is empty."]));

        var result = await _sut.InstallAsync(new MemoryStream(), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.PACKAGE_INVALID", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        _repository.Verify(r => r.AddAsync(It.IsAny<Plugin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Install_WhenAlreadyInstalled_ReturnsConflictAndRollsBackRuntime()
    {
        SetupInstallSucceeds();
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("asset-importer", PluginManager.EnabledStatus));

        var result = await _sut.InstallAsync(new MemoryStream([1]), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.ALREADY_INSTALLED", result.Error!.Code);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        _manager.Verify(m => m.UninstallAsync("asset-importer", It.IsAny<CancellationToken>()), Times.Once,
            "A rejected re-install must not leave the runtime loaded.");
    }

    [Fact]
    public async Task Install_AfterUninstall_CreatesFreshRow()
    {
        SetupInstallSucceeds();
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("asset-importer", PluginManager.UninstalledStatus));

        var result = await _sut.InstallAsync(new MemoryStream([1]), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginManager.EnabledStatus, result.Value!.Status);
    }

    // ---- Get / List --------------------------------------------------------------------------------

    [Fact]
    public async Task Get_UnknownPluginId_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByPluginIdAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Plugin?)null);

        var result = await _sut.GetAsync("ghost");

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.NOT_FOUND", result.Error!.Code);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task Get_InstalledPlugin_ReturnsRow()
    {
        var row = Row("asset-importer", PluginManager.EnabledStatus);
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        var result = await _sut.GetAsync("asset-importer");

        Assert.True(result.IsSuccess);
        Assert.Same(row, result.Value);
    }

    [Fact]
    public async Task List_DelegatesToRepository()
    {
        var rows = new[] { Row("a-plugin", PluginManager.EnabledStatus) };
        _repository.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows);

        var list = await _sut.ListAsync();

        Assert.Single(list);
    }

    // ---- Enable ------------------------------------------------------------------------------------

    [Fact]
    public async Task Enable_FromDisabled_ReloadsPackageAndPersistsStatus()
    {
        var row = Row("asset-importer", PluginManager.DisabledStatus);
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);
        _manager.Setup(m => m.InstallFromDirectoryAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PluginManager.InstallOutcome.Succeeded(new PluginManifest()));

        var result = await _sut.EnableAsync("asset-importer", ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginManager.EnabledStatus, row.Status);
        Assert.Equal(ActorId, row.UpdatedById);
        _repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Enable_WhenAlreadyEnabled_ReturnsConflictWithoutReload()
    {
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("asset-importer", PluginManager.EnabledStatus));

        var result = await _sut.EnableAsync("asset-importer", ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.INVALID_STATE", result.Error!.Code);
        _manager.Verify(m => m.InstallFromDirectoryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Enable_WhenPackageMissingOnDisk_ReturnsValidationError()
    {
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("asset-importer", PluginManager.DisabledStatus));
        _manager.Setup(m => m.InstallFromDirectoryAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PluginManager.InstallOutcome.Failed(["missing on disk"]));

        var result = await _sut.EnableAsync("asset-importer", ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.PACKAGE_INVALID", result.Error!.Code);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task Enable_UnknownPlugin_ReturnsNotFound()
    {
        _repository.Setup(r => r.GetByPluginIdAsync("ghost", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Plugin?)null);

        var result = await _sut.EnableAsync("ghost", ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.NOT_FOUND", result.Error!.Code);
    }

    // ---- Disable -------------------------------------------------------------------------------------

    [Fact]
    public async Task Disable_FromEnabled_UnloadsAndPersistsStatus()
    {
        var row = Row("asset-importer", PluginManager.EnabledStatus);
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        var result = await _sut.DisableAsync("asset-importer", ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginManager.DisabledStatus, row.Status);
        _manager.Verify(m => m.DisableAsync("asset-importer"), Times.Once);
    }

    [Fact]
    public async Task Disable_WhenAlreadyDisabled_ReturnsConflict()
    {
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("asset-importer", PluginManager.DisabledStatus));

        var result = await _sut.DisableAsync("asset-importer", ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.INVALID_STATE", result.Error!.Code);
        _manager.Verify(m => m.DisableAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Disable_WhenUninstalled_ReturnsConflict()
    {
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("asset-importer", PluginManager.UninstalledStatus));

        var result = await _sut.DisableAsync("asset-importer", ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.INVALID_STATE", result.Error!.Code);
        _manager.Verify(m => m.DisableAsync(It.IsAny<string>()), Times.Never);
    }

    // ---- Uninstall -----------------------------------------------------------------------------------

    [Fact]
    public async Task Uninstall_FromEnabled_DropsRuntimeDirectorySchemaAndPersistsTombstone()
    {
        var row = Row("asset-importer", PluginManager.EnabledStatus);
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(row);

        var result = await _sut.UninstallAsync("asset-importer", ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(PluginManager.UninstalledStatus, row.Status);
        _manager.Verify(m => m.UninstallAsync("asset-importer", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Uninstall_WhenAlreadyUninstalled_ReturnsConflict()
    {
        _repository.Setup(r => r.GetByPluginIdAsync("asset-importer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Row("asset-importer", PluginManager.UninstalledStatus));

        var result = await _sut.UninstallAsync("asset-importer", ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("PLUGINS.INVALID_STATE", result.Error!.Code);
        _manager.Verify(m => m.UninstallAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Menu items ------------------------------------------------------------------------------------

    [Fact]
    public void GetMenuItems_DelegatesToManager()
    {
        var items = new[] { new PluginMenuItem("Assets", "/plugins/assets", "extension", 10, null) };
        _manager.SetupGet(m => m.MenuItems).Returns(items);

        var result = _sut.GetMenuItems();

        Assert.Same(items, result);
    }
}
