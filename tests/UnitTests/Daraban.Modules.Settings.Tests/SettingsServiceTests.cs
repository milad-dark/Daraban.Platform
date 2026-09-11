using Daraban.Modules.Settings.Data;
using Daraban.Modules.Settings.Data.Entities;
using Daraban.Modules.Settings.Services;
using Daraban.Modules.Settings.Services.Dtos;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Daraban.Modules.Settings.Tests;

/// <summary>
/// SettingsService (Task 7.4): masking at the DTO boundary, the mask round-trip rule,
/// validation wiring and category routing of the connectivity tests. The cache is mocked
/// (its DB choreography is not what's under test); the connectivity tester is mocked so no
/// test ever opens a real socket.
/// </summary>
public class SettingsServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Mock<IConnectivityTester> _tester = new();
    private readonly FakeCache _cache = new();
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _cache.Seed();
        _sut = new SettingsService(_cache, _tester.Object, Mock.Of<ILogger<SettingsService>>());
    }

    // ---- Listing & masking -----------------------------------------------------------------

    [Fact]
    public async Task GetAll_Masks_Secret_Values()
    {
        var result = await _sut.GetAllAsync();

        Assert.True(result.IsSuccess);
        var email = result.Value!.Categories.Single(c => c.Category == "email");
        var password = email.Settings.Single(s => s.Key == "email.smtp_password");

        Assert.True(password.IsSecret);
        Assert.Equal(SettingsService.SecretMask, password.Value);
    }

    [Fact]
    public async Task GetAll_Never_Masks_Non_Secret_Values()
    {
        var result = await _sut.GetAllAsync();

        var branding = result.Value!.Categories.Single(c => c.Category == "branding");
        var appName = branding.Settings.Single(s => s.Key == "branding.application_name");

        Assert.False(appName.IsSecret);
        Assert.Equal("Daraban Platform", appName.Value);
    }

    [Fact]
    public async Task GetAll_Groups_Every_Setting_Into_A_Known_Category()
    {
        var result = await _sut.GetAllAsync();

        var totalListed = result.Value!.Categories.Sum(c => c.Settings.Count);
        Assert.Equal(SettingCatalog.All.Length, totalListed);
    }

    // ---- Updates -----------------------------------------------------------------------------

    [Fact]
    public async Task Update_Persists_A_Valid_Value()
    {
        var result = await _sut.UpdateAsync("branding.application_name", new UpdateSettingRequest("Acme IT"), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme IT", result.Value!.Value);
        Assert.Equal("Acme IT", _cache.Snapshot.Single(s => s.Key == "branding.application_name").Value);
    }

    [Fact]
    public async Task Update_Rejects_An_Unknown_Key_As_NotFound()
    {
        var result = await _sut.UpdateAsync("nope.nope", new UpdateSettingRequest("x"), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SETTINGS.KEY_UNKNOWN", result.Error!.Code);
    }

    [Fact]
    public async Task Update_Rejects_An_Invalid_Value_As_Validation()
    {
        var result = await _sut.UpdateAsync("email.smtp_port", new UpdateSettingRequest("not-a-port"), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SETTINGS.INVALID_INT", result.Error!.Code);
    }

    [Fact]
    public async Task Update_Treats_The_Mask_As_Keep_Current_For_Secrets()
    {
        // The admin opened the form and saved without touching the password field: the UI
        // round-trips the mask. That must NOT overwrite the stored secret with bullets.
        var result = await _sut.UpdateAsync("email.smtp_password", new UpdateSettingRequest(SettingsService.SecretMask), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("real-secret", _cache.Snapshot.Single(s => s.Key == "email.smtp_password").Value);
    }

    [Fact]
    public async Task Update_Still_Masks_The_Response_After_A_Successful_Secret_Write()
    {
        var result = await _sut.UpdateAsync("email.smtp_password", new UpdateSettingRequest("new-secret"), ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(SettingsService.SecretMask, result.Value!.Value);
        Assert.Equal("new-secret", _cache.Snapshot.Single(s => s.Key == "email.smtp_password").Value);
    }

    [Fact]
    public async Task Update_Rejects_The_Mask_Literally_On_A_Non_Secret_Key()
    {
        // Mask semantics apply to secrets only. The mask glyphs are non-ASCII, so on a
        // normal text key they are rejected by the printable-ASCII floor -- coherent,
        // because a branding field holding bullets is a mistake, not a credential.
        var result = await _sut.UpdateAsync("branding.application_name", new UpdateSettingRequest(SettingsService.SecretMask), ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("SETTINGS.INVALID_CHARACTERS", result.Error!.Code);
    }

    // ---- Connectivity tests ---------------------------------------------------------------------

    [Fact]
    public async Task TestConnection_Routes_Email_Keys_To_The_Smtp_Tester()
    {
        _cache.Update("email.smtp_host", "smtp.example.com"); // test requires a configured host
        _tester
            .Setup(t => t.TestEmailAsync(It.IsAny<SmtpSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionTestResultDto(true, "ok", 12));

        var result = await _sut.TestConnectionAsync("email.smtp_host");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Success);
        _tester.Verify(t => t.TestEmailAsync(It.IsAny<SmtpSettings>(), It.IsAny<CancellationToken>()), Times.Once);
        _tester.Verify(t => t.TestLdapAsync(It.IsAny<LdapConnectionSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TestConnection_Routes_Ldap_Keys_To_The_Ldap_Tester()
    {
        _cache.Update("ldap.server", "ldap.example.com"); // test requires a configured server
        _tester
            .Setup(t => t.TestLdapAsync(It.IsAny<LdapConnectionSettings>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConnectionTestResultDto(true, "ok", 5));

        var result = await _sut.TestConnectionAsync("ldap.base_dn");

        Assert.True(result.IsSuccess);
        _tester.Verify(t => t.TestLdapAsync(It.IsAny<LdapConnectionSettings>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TestConnection_Fails_For_Categories_Without_A_Test()
    {
        var result = await _sut.TestConnectionAsync("branding.application_name");

        Assert.False(result.IsSuccess);
        Assert.Equal("SETTINGS.TEST_NOT_SUPPORTED", result.Error!.Code);
    }

    [Fact]
    public async Task TestConnection_Fails_For_Unknown_Keys()
    {
        var result = await _sut.TestConnectionAsync("nope.nope");

        Assert.False(result.IsSuccess);
        Assert.Equal("SETTINGS.KEY_UNKNOWN", result.Error!.Code);
    }

    [Fact]
    public async Task TestConnection_Reports_An_Unconfigured_Email_Host_As_Validation()
    {
        _cache.Update("email.smtp_host", "");

        var result = await _sut.TestConnectionAsync("email.smtp_host");

        Assert.False(result.IsSuccess);
        Assert.Equal("SETTINGS.TEST_NOT_CONFIGURED", result.Error!.Code);
        _tester.Verify(t => t.TestEmailAsync(It.IsAny<SmtpSettings>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>In-memory stand-in for SystemSettingCache: the real cache's DB+Redis
    /// choreography is integration surface, not unit surface. Seeded from the catalog
    /// exactly like the startup seeder would.</summary>
    private sealed class FakeCache : SystemSettingCache
    {
        public FakeCache() : base(
            new FakeScopeFactory(),
            new NoOpCache(),
            Mock.Of<ILogger<SystemSettingCache>>())
        {
        }

        public List<SystemSetting> Snapshot { get; } = new();

        public void Seed()
        {
            Snapshot.AddRange(SettingCatalog.All.Select(d => new SystemSetting
            {
                Key = d.Key,
                Value = d.Key == "email.smtp_password" ? "real-secret" : d.Default,
                ValueType = d.Type.ToString().ToLowerInvariant(),
                Category = d.Category,
                Description = d.Description,
                IsSecret = d.IsSecret,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            }));
        }

        public void Update(string key, string value)
        {
            Snapshot.Single(s => s.Key == key).Value = value;
        }

        public override Task EnsureLoadedAsync(CancellationToken ct = default) => Task.CompletedTask;

        public override Task UpdateAsync(string key, string newValue, Guid actorId, CancellationToken ct = default)
        {
            Snapshot.Single(s => s.Key == key).Value = newValue;
            return Task.CompletedTask;
        }

        public override IReadOnlyList<SystemSetting> All => Snapshot;

        public override SystemSetting? Get(string key) => Snapshot.FirstOrDefault(s => s.Key == key);

        private sealed class FakeScopeFactory : IServiceScopeFactory
        {
            public IServiceScope CreateScope() => throw new NotSupportedException("Not used by FakeCache.");
        }

        private sealed class NoOpCache : IDistributedCache
        {
            public byte[]? Get(string key) => null;
            public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
            public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
            public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
            public void Refresh(string key) { }
            public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
            public void Remove(string key) { }
            public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        }
    }
}
