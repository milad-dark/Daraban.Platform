using System.Security.Cryptography;
using Daraban.Modules.Discovery.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Daraban.Modules.Discovery.Tests;

/// <summary>
/// CredentialEncryptionService with a real key and real AES-GCM: these tests encrypt actual
/// plaintext and decrypt actual ciphertext, because a mock would prove nothing about the one
/// property that matters -- that SNMP community strings and v3 passphrases stored in the
/// database are unreadable without the key, and tamper-evident with it.
/// </summary>
public class CredentialEncryptionServiceTests : IDisposable
{
    private static string RandomKeyBase64()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private readonly string _keyBase64 = RandomKeyBase64();

    private CredentialEncryptionService CreateSut()
        => new(BuildConfiguration(_keyBase64));

    private static IConfiguration BuildConfiguration(string key)
    {
        var values = new Dictionary<string, string?> { ["Encryption:CredentialKey"] = key };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    public void Dispose()
    {
        // Nothing holding unmanaged resources; the key is a managed byte[].
        GC.SuppressFinalize(this);
    }

    // ---- Round trip --------------------------------------------------------------------------

    [Theory]
    [InlineData("public")]
    [InlineData("privateCommunity$3cret")]
    [InlineData("Ünïcödé pässphräsé — SNMPv3 priv")]   // non-ASCII survives the UTF-8 round trip
    [InlineData("x")]                                   // single character
    [InlineData("A 256-bit key is enough, but the plaintext can be any length at all, so here is a much longer passphrase to prove it")]
    public void Encrypt_Decrypt_RoundTrips_The_Plaintext(string plain)
    {
        var sut = CreateSut();

        var cipher = sut.Encrypt(plain);

        Assert.NotEqual(plain, cipher);
        Assert.Equal(plain, sut.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_Produces_A_Different_CipherText_Every_Call()
    {
        var sut = CreateSut();

        var first = sut.Encrypt("public");
        var second = sut.Encrypt("public");

        // A fresh 96-bit nonce per encryption: identical plaintexts must never yield identical
        // ciphertexts, or an attacker can correlate credentials across rows without decrypting
        // anything.
        Assert.NotEqual(first, second);
        Assert.Equal(sut.Decrypt(first), sut.Decrypt(second));
    }

    [Fact]
    public void Encrypt_Passes_An_Empty_String_Through_Untouched()
    {
        // Nothing to protect; no reason to mint a nonce for it.
        Assert.Equal(string.Empty, CreateSut().Encrypt(string.Empty));
        Assert.Equal(string.Empty, CreateSut().Decrypt(string.Empty));
    }

    [Fact]
    public void Encrypt_Encrypts_A_Whitespace_Only_Value()
    {
        var sut = CreateSut();

        // " " is NOT empty -- a credential that is literally spaces is still data, and passes
        // through the same protection as anything else. (IsNullOrWhiteSpace would have been wrong
        // here: it would have stored a "secure" value that is trivially guessable.)
        var cipher = sut.Encrypt("   ");

        Assert.NotEqual("   ", cipher);
        Assert.Equal("   ", sut.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_Output_Is_Base64_And_Carries_Nonce_Tag_And_Ciphertext()
    {
        var cipher = CreateSut().Encrypt("public");

        var bytes = Convert.FromBase64String(cipher);

        // 12-byte nonce + 16-byte tag + 6-byte "public".
        Assert.Equal(12 + 16 + 6, bytes.Length);
    }

    // ---- Key handling ------------------------------------------------------------------------

    [Fact]
    public void Constructor_Throws_When_The_Key_Is_Missing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        // Fail fast beats fail open: silently generating a key would make every stored
        // credential undecryptable after a restart.
        var ex = Assert.Throws<InvalidOperationException>(() => new CredentialEncryptionService(config));
        Assert.Contains("CredentialKey", ex.Message);
    }

    [Fact]
    public void Constructor_Throws_When_The_Key_Is_The_Wrong_Length()
    {
        var shortKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            new CredentialEncryptionService(BuildConfiguration(shortKey)));

        Assert.Contains("256", ex.Message);
    }

    [Fact]
    public void Decrypt_With_The_Wrong_Key_Fails()
    {
        var cipher = CreateSut().Encrypt("public");

        // A second service with an independent key -- as another process/replica would have.
        var other = new CredentialEncryptionService(BuildConfiguration(RandomKeyBase64()));

        // AES-GCM authenticates the tag: the wrong key cannot silently yield garbage, it throws.
        Assert.ThrowsAny<CryptographicException>(() => other.Decrypt(cipher));
    }

    [Fact]
    public void Decrypt_Rejects_Truncated_Input()
    {
        var sut = CreateSut();

        var cipher = sut.Encrypt("public");
        var bytes = Convert.FromBase64String(cipher);

        // Chop off the tag: shorter than nonce+tag is a format error before crypto even runs.
        var truncated = Convert.ToBase64String(bytes[..15]);
        Assert.ThrowsAny<CryptographicException>(() => sut.Decrypt(truncated));

        // Chop off one byte of the ciphertext: right length class, but the tag no longer matches.
        var tampered = Convert.ToBase64String(bytes[..^1]);
        Assert.ThrowsAny<CryptographicException>(() => sut.Decrypt(tampered));
    }

    [Theory]
    [InlineData("not base64 at all!!")]
    [InlineData("aGVsbG8=")] // valid base64, wrong structure entirely
    public void Decrypt_Rejects_Non_Ciphertext_Input(string garbage)
    {
        // Malformed base64 used to escape as a raw FormatException from Convert.FromBase64String;
        // the service now wraps it, so every failure mode of Decrypt is a CryptographicException.
        Assert.ThrowsAny<CryptographicException>(() => CreateSut().Decrypt(garbage));
    }

    [Fact]
    public void Decrypt_Detects_A_Flipped_Bit_In_The_Ciphertext()
    {
        var sut = CreateSut();

        var cipher = sut.Encrypt("public");
        var bytes = Convert.FromBase64String(cipher);

        // Flip one bit inside the ciphertext region (past nonce+tag).
        bytes[12 + 16] ^= 0x01;

        // The GCM tag is a MAC over the ciphertext: any modification is detected, not decrypted.
        Assert.ThrowsAny<CryptographicException>(() => sut.Decrypt(Convert.ToBase64String(bytes)));
    }

    [Fact]
    public void CipherText_Leaks_Nothing_About_The_Plaintext()
    {
        var sut = CreateSut();

        var cipher = sut.Encrypt("super-secret-snmp-community");

        // The whole point of the exercise: a database dump alone never yields a credential.
        Assert.DoesNotContain("super-secret", cipher);
        Assert.DoesNotContain("snmp", cipher);
    }
}
