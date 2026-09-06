using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Services.Auth;
using Daraban.Platform.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Daraban.Modules.Identity.Tests;

/// <summary>
/// JwtTokenService and JwtSigningKeyProvider. Tokens are issued with a real RSA key and then
/// parsed back, so the claim shape and the RS256 signature are asserted against what a validator
/// would actually see -- not against the code's own intentions.
/// </summary>
public class JwtTokenServiceTests
{
    private const string Issuer = "https://daraban.test";
    private const string Audience = "daraban-api";

    private static readonly Guid ActiveEntityId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly JwtSigningKeyProvider _keyProvider;

    public JwtTokenServiceTests()
    {
        // Development environment => the provider self-generates an ephemeral key, which is exactly
        // what a unit test wants: no PEM on disk, no shared state between test classes.
        _keyProvider = new JwtSigningKeyProvider(
            Options.Create(new JwtOptions()),
            new FakeHostEnvironment("Development"),
            NullLogger<JwtSigningKeyProvider>.Instance);
    }

    private JwtTokenService CreateSut(int lifetimeMinutes = 15)
        => new(
            Options.Create(new JwtOptions
            {
                Issuer = Issuer,
                Audience = Audience,
                AccessTokenLifetimeMinutes = lifetimeMinutes,
            }),
            _keyProvider);

    private static User UserWith(int tokenVersion = 0) => new()
    {
        Id = Guid.CreateVersion7(),
        Username = "alice",
        Email = "alice@example.com",
        DisplayName = "Alice",
        TokenVersion = tokenVersion,
    };

    private static JwtSecurityToken Parse(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);

    // ---- Claim shape -------------------------------------------------------------------------

    [Fact]
    public void IssueAccessToken_Emits_The_Documented_Claims()
    {
        var user = UserWith(tokenVersion: 7);

        var (token, _) = CreateSut().IssueAccessToken(user, ActiveEntityId);
        var parsed = Parse(token);

        Assert.Equal(user.Id.ToString(), parsed.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal(ActiveEntityId.ToString(), parsed.Claims.Single(c => c.Type == "active_entity_id").Value);
        Assert.Equal("alice", parsed.Claims.Single(c => c.Type == "name").Value);
        Assert.Equal("alice@example.com", parsed.Claims.Single(c => c.Type == "email").Value);

        // token_version backs immediate revocation (Task 1.3 SS8) -- Host.Api compares it against
        // the stored value on every request.
        Assert.Equal("7", parsed.Claims.Single(c => c.Type == "token_version").Value);
    }

    [Fact]
    public void IssueAccessToken_Never_Embeds_The_Password_Hash()
    {
        var user = UserWith();
        user.PasswordHash = "AQAAAAIAAYagAAAAE-not-a-real-hash";

        var (token, _) = CreateSut().IssueAccessToken(user, ActiveEntityId);

        // A JWT payload is base64, not encryption -- anyone holding the token can read every claim.
        Assert.DoesNotContain("AQAAAAIAAYag", token);
        Assert.DoesNotContain("PasswordHash", Parse(token).Claims.Select(c => c.Type));
    }

    [Fact]
    public void IssueAccessToken_Gives_Every_Token_A_Unique_Jti()
    {
        var user = UserWith();
        var sut = CreateSut();

        var first = Parse(sut.IssueAccessToken(user, ActiveEntityId).Token);
        var second = Parse(sut.IssueAccessToken(user, ActiveEntityId).Token);

        // jti is what makes a specific token identifiable in logs and in any future denylist.
        Assert.NotEqual(
            first.Claims.Single(c => c.Type == "jti").Value,
            second.Claims.Single(c => c.Type == "jti").Value);
    }

    [Fact]
    public void IssueAccessToken_Sets_The_Configured_Issuer_And_Audience()
    {
        var parsed = Parse(CreateSut().IssueAccessToken(UserWith(), ActiveEntityId).Token);

        Assert.Equal(Issuer, parsed.Issuer);
        Assert.Contains(Audience, parsed.Audiences);
    }

    [Fact]
    public void IssueAccessToken_Signs_With_RS256()
    {
        var parsed = Parse(CreateSut().IssueAccessToken(UserWith(), ActiveEntityId).Token);

        // Asymmetric by design (Task 1.3 SS2): no shared secret between the signer and any process
        // that only needs to validate. An accidental downgrade to HS256 would be a real weakening.
        Assert.Equal(SecurityAlgorithms.RsaSha256, parsed.Header.Alg);
    }

    // ---- Lifetime ----------------------------------------------------------------------------

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(60)]
    public void IssueAccessToken_Honours_The_Configured_Lifetime(int minutes)
    {
        var before = DateTimeOffset.UtcNow;

        var (_, expiresAt) = CreateSut(minutes).IssueAccessToken(UserWith(), ActiveEntityId);

        var expected = before.AddMinutes(minutes);
        Assert.InRange(expiresAt, expected.AddSeconds(-5), expected.AddSeconds(5));
    }

    [Fact]
    public void IssueAccessToken_Reports_An_Expiry_Matching_The_Token_Itself()
    {
        var (token, expiresAt) = CreateSut().IssueAccessToken(UserWith(), ActiveEntityId);
        var parsed = Parse(token);

        // The caller sends expiresAt to the client so it knows when to refresh. If it disagreed with
        // the token's own exp, the client would refresh too late and hit 401s.
        Assert.Equal(expiresAt.UtcDateTime.ToString("s"), parsed.ValidTo.ToString("s"));
    }

    [Fact]
    public void IssueAccessToken_Is_Not_Valid_Before_Now()
    {
        var parsed = Parse(CreateSut().IssueAccessToken(UserWith(), ActiveEntityId).Token);

        Assert.True(parsed.ValidFrom <= DateTime.UtcNow.AddSeconds(5));
        Assert.True(parsed.ValidTo > parsed.ValidFrom);
    }

    // ---- Signature verification ---------------------------------------------------------------

    [Fact]
    public void An_Issued_Token_Validates_Against_The_Providers_Key()
    {
        var user = UserWith();
        var (token, _) = CreateSut().IssueAccessToken(user, ActiveEntityId);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(_keyProvider.GetKey()),
        }, out _);

        Assert.Equal(user.Id.ToString(), principal.FindFirst("sub")!.Value);
    }

    [Fact]
    public void A_Token_Signed_By_A_Different_Key_Is_Rejected()
    {
        var (token, _) = CreateSut().IssueAccessToken(UserWith(), ActiveEntityId);

        using var otherKey = RSA.Create(3072);
        var handler = new JwtSecurityTokenHandler();

        // Guards the whole point of signing: a token minted elsewhere must not be accepted.
        Assert.ThrowsAny<SecurityTokenException>(() => handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(otherKey),
        }, out _));
    }

    [Fact]
    public void A_Tampered_Payload_Fails_Validation()
    {
        var (token, _) = CreateSut().IssueAccessToken(UserWith(), ActiveEntityId);

        // Flip a character in the payload segment, leaving the signature untouched.
        var parts = token.Split('.');
        var payload = parts[1].ToCharArray();
        payload[0] = payload[0] == 'e' ? 'f' : 'e';
        var tampered = $"{parts[0]}.{new string(payload)}.{parts[2]}";

        var handler = new JwtSecurityTokenHandler();
        Assert.ThrowsAny<Exception>(() => handler.ValidateToken(tampered, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(_keyProvider.GetKey()),
        }, out _));
    }

    // ---- Signing key provider ------------------------------------------------------------------

    [Fact]
    public void KeyProvider_Returns_The_Same_Instance_Every_Time()
    {
        var provider = new JwtSigningKeyProvider(
            Options.Create(new JwtOptions()),
            new FakeHostEnvironment("Development"),
            NullLogger<JwtSigningKeyProvider>.Instance);

        // Registered as a singleton precisely so the signer and the validator in one process share
        // one key. If this handed out a new key per call, every token would fail validation.
        Assert.Same(provider.GetKey(), provider.GetKey());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void KeyProvider_Refuses_To_Generate_An_Ephemeral_Key_Outside_Development(string environment)
    {
        var provider = new JwtSigningKeyProvider(
            Options.Create(new JwtOptions { SigningKeyPemPath = null }),
            new FakeHostEnvironment(environment),
            NullLogger<JwtSigningKeyProvider>.Instance);

        // Fail fast beats fail open: an unreproducible key would make every token unverifiable
        // after a restart or on a second replica, while appearing to work in a demo.
        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetKey());
        Assert.Contains("SigningKeyPemPath", ex.Message);
    }

    [Fact]
    public void KeyProvider_Loads_A_Configured_Pem_File()
    {
        using var rsa = RSA.Create(3072);
        var path = Path.Combine(Path.GetTempPath(), $"daraban-jwt-test-{Guid.CreateVersion7()}.pem");
        File.WriteAllText(path, rsa.ExportRSAPrivateKeyPem());

        try
        {
            var provider = new JwtSigningKeyProvider(
                Options.Create(new JwtOptions { SigningKeyPemPath = path }),
                // Production, to prove the PEM path is what satisfies the guard above.
                new FakeHostEnvironment("Production"),
                NullLogger<JwtSigningKeyProvider>.Instance);

            var loaded = provider.GetKey();

            Assert.Equal(
                Convert.ToBase64String(rsa.ExportRSAPublicKey()),
                Convert.ToBase64String(loaded.ExportRSAPublicKey()));

            provider.Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Minimal IHostEnvironment so the provider's environment branch can be exercised
    /// without spinning up a host.</summary>
    private sealed class FakeHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Daraban.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
