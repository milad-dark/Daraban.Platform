using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Daraban.Modules.Discovery.Services;

/// <summary>
/// Encrypts and decrypts sensitive SNMP credentials at rest (Task 5.1).
/// Uses AES-256-GCM for authenticated encryption.
/// </summary>
public interface ICredentialEncryptionService
{
    /// <summary>Encrypt a plaintext credential value.</summary>
    string Encrypt(string plainText);

    /// <summary>Decrypt an encrypted credential value.</summary>
    string Decrypt(string cipherText);
}

/// <summary>
/// AES-256-GCM encryption for SNMP credentials.
/// Key should be stored in Azure Key Vault, DPAPI, or environment variable.
/// </summary>
public class CredentialEncryptionService : ICredentialEncryptionService
{
    private readonly byte[] _key;
    private const int KeySize = 32; // 256 bits
    private const int NonceSize = 12; // 96 bits for GCM
    private const int TagSize = 16; // 128 bits for GCM

    public CredentialEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Encryption:CredentialKey"]
            ?? throw new InvalidOperationException("Encryption:CredentialKey not configured.");

        _key = Convert.FromBase64String(keyBase64);
        if (_key.Length != KeySize)
            throw new InvalidOperationException($"Encryption key must be {KeySize} bytes (256 bits).");
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Format: base64(nonce + tag + ciphertext)
        var result = new byte[NonceSize + TagSize + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize + TagSize, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        var data = Convert.FromBase64String(cipherText);
        if (data.Length < NonceSize + TagSize)
            throw new CryptographicException("Invalid cipher text format.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertext = new byte[data.Length - NonceSize - TagSize];

        Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(data, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(data, NonceSize + TagSize, ciphertext, 0, ciphertext.Length);

        var plaintext = new byte[ciphertext.Length];
        using var aesGcm = new AesGcm(_key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
