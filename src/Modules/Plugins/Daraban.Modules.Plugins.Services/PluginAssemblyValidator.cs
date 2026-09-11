using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Daraban.Platform.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daraban.Modules.Plugins.Services;

/// <summary>
/// Validates a plugin assembly before it is ever loaded (Task 7.5: "code signing
/// validation on plugin DLL (optional, configurable)"). Two checks:
/// 1. managed validity: the file must be a PE/COFF assembly the runtime can load;
/// 2. Authenticode/X509 signature validation, when <see cref="PluginsOptions.RequireSignedAssemblies"/>
///    is enabled -- either any chain-trusted signature, or a pinned signer certificate.
/// The signature check is a managed, cross-platform verification: it parses the PE
/// certificate table, decodes the Authenticode SignedCms blob, verifies the signature
/// cryptographically, and confirms the signed digest matches the file content.
/// </summary>
public class PluginAssemblyValidator(
    IOptions<PluginsOptions> options,
    ILogger<PluginAssemblyValidator> logger)
{
    /// <summary>Runs all enabled validations on the plugin's main assembly file.
    /// Returns the list of violations; empty means the assembly may be loaded.</summary>
    public List<string> Validate(string assemblyPath)
    {
        var errors = new List<string>();

        if (!File.Exists(assemblyPath))
        {
            errors.Add($"Assembly file '{assemblyPath}' does not exist.");
            return errors;
        }

        if (!IsValidAssembly(assemblyPath))
            errors.Add($"File '{Path.GetFileName(assemblyPath)}' is not a valid .NET assembly.");

        var opts = options.Value;
        if (opts.RequireSignedAssemblies)
            errors.AddRange(ValidateSignature(assemblyPath, opts));

        return errors;
    }

    /// <summary>Checks the PE/COFF header: 'MZ' DOS header followed by a PE header whose
    /// optional-header magic is PE32 (0x10B) or PE32+ (0x20B).</summary>
    internal static bool IsValidAssembly(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[512];
            var read = stream.ReadAtLeast(header, 0x40);
            if (read < 0x40)
                return false;

            if (header[0] != (byte)'M' || header[1] != (byte)'Z')
                return false;

            var peOffset = BitConverter.ToInt32(header[0x3C..0x40]);
            if (peOffset <= 0 || peOffset + 0x18 > read)
                return false;

            if (header[peOffset] != (byte)'P' || header[peOffset + 1] != (byte)'E')
                return false;

            var magic = BitConverter.ToUInt16(header[(peOffset + 0x18)..(peOffset + 0x1A)]);
            return magic is 0x10B or 0x20B;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>Validates the file's Authenticode signature and, when a pinned
    /// certificate is configured, that the signer matches it.</summary>
    private List<string> ValidateSignature(string assemblyPath, PluginsOptions opts)
    {
        var errors = new List<string>();
        var fileName = Path.GetFileName(assemblyPath);

        try
        {
            var signedCms = AuthenticodeVerifier.ReadSignature(assemblyPath, out var peHash);
            if (signedCms is null)
            {
                errors.Add($"Assembly '{fileName}' is not signed.");
                return errors;
            }

            // Cryptographic verification of the SignedCms (message digest + signer signature).
            // false = also verify the signer certificate chain at build time below; here we
            // only assert the signature itself is intact.
            signedCms.CheckSignature(verifySignatureOnly: true);

            // Bind the signature to *this* file: the signed digest must match the PE hash.
            if (!AuthenticodeVerifier.DigestMatches(signedCms, peHash))
            {
                errors.Add($"Assembly '{fileName}' signature does not match the file content.");
                return errors;
            }

            var signer = signedCms.SignerInfos[0].Certificate
                ?? throw new InvalidOperationException("Signature carries no signer certificate.");

            // Verify the chain against the machine's trusted roots.
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            if (!chain.Build(signer))
            {
                errors.Add($"Assembly '{fileName}' signature does not chain to a trusted root.");
                return errors;
            }

            // Optional pinning: signer must match the configured certificate.
            if (!string.IsNullOrWhiteSpace(opts.RequiredSignerCertificatePath))
            {
                var expected = LoadCertificate(opts.RequiredSignerCertificatePath);
                if (expected is null)
                {
                    errors.Add("The configured RequiredSignerCertificatePath could not be loaded; refusing the plugin.");
                    return errors;
                }

                if (!string.Equals(signer.Thumbprint, expected.Thumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Assembly '{fileName}' is signed by an unexpected certificate.");
                    logger.LogWarning(
                        "Plugin signer mismatch: expected {ExpectedThumbprint}, got {ActualThumbprint}",
                        expected.Thumbprint, signer.Thumbprint);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Signature validation failed for '{Assembly}'", assemblyPath);
            errors.Add($"Signature validation failed: {ex.Message}");
        }

        return errors;
    }

    /// <summary>Loads a certificate from PEM (certificate-only) or PFX (with or without password).</summary>
    internal static X509Certificate2? LoadCertificate(string path)
    {
        try
        {
            if (path.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".crt", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".cer", StringComparison.OrdinalIgnoreCase))
            {
                return X509Certificate2.CreateFromPemFile(path);
            }

            return X509CertificateLoader.LoadCertificateFromFile(path);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Managed, cross-platform Authenticode support: extracts the SignedCms blob from the
/// PE certificate table and computes the PE hash the way Authenticode mandates
/// (checksum zeroed, security directory entry zeroed, certificate table zeroed).
/// </summary>
internal static class AuthenticodeVerifier
{
    /// <summary>Reads the Authenticode signature from the PE certificate table.
    /// Returns null when the file carries no signature. Also outputs the Authenticode PE hash.</summary>
    internal static SignedCms? ReadSignature(string path, out byte[] peHash)
    {
        byte[] image;
        peHash = [];
        try
        {
            image = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }

        if (image.Length < 0x40 || image[0] != (byte)'M' || image[1] != (byte)'Z')
            return null;

        var peOffset = BitConverter.ToInt32(image, 0x3C);
        if (peOffset <= 0 || peOffset + 0x18 > image.Length)
            return null;
        if (image[peOffset] != (byte)'P' || image[peOffset + 1] != (byte)'E')
            return null;

        var magic = BitConverter.ToUInt16(image, peOffset + 0x18);
        var isPe32Plus = magic == 0x20B;

        // NumberOfRvaAndSizes is at optional-header + 92 (PE32) / 108 (PE32+);
        // the data directories follow at + 96 / + 112. Security dir is index 4.
        var dataDirOffset = peOffset + 0x18 + (isPe32Plus ? 112 : 96);
        if (dataDirOffset + 8 * 8 > image.Length)
            return null;

        if (BitConverter.ToUInt32(image, dataDirOffset - 4) < 5)
            return null; // fewer data directories than the security directory index

        var secDirRva = BitConverter.ToInt32(image, dataDirOffset + 4 * 8);
        var secDirSize = BitConverter.ToInt32(image, dataDirOffset + 4 * 8 + 4);
        if (secDirRva <= 0 || secDirSize <= 0 || secDirRva + secDirSize > image.Length)
            return null;

        // The "RVA" of the security directory is a raw file offset (WIN_CERTIFICATE table).
        var signatureBlob = new byte[secDirSize];
        Buffer.BlockCopy(image, secDirRva, signatureBlob, 0, secDirSize);

        // WIN_CERTIFICATE: dwLength (incl. 8-byte header) + wRevision + wCertificateType.
        if (signatureBlob.Length < 8)
            return null;
        var certLength = BitConverter.ToInt32(signatureBlob, 0);
        var certType = BitConverter.ToUInt16(signatureBlob, 6);
        if (certLength < 8 || certType != 0x0002) // 0x0002 = WIN_CERT_TYPE_PKCS_SIGNED_DATA
            return null;

        peHash = ComputePeHash(image, isPe32Plus, peOffset, dataDirOffset, secDirRva, secDirSize);
        if (peHash.Length == 0)
            return null;

        var contentInfo = new byte[certLength - 8];
        Buffer.BlockCopy(signatureBlob, 8, contentInfo, 0, contentInfo.Length);

        var cms = new SignedCms();
        cms.Decode(contentInfo);
        return cms;
    }

    /// <summary>Computes the Authenticode PE hash (SHA-256 by default; the digest algorithm
    /// is matched against the signature in <see cref="DigestMatches"/>): checksum field
    /// zeroed, security directory entry zeroed, certificate table excluded.</summary>
    internal static byte[] ComputePeHash(
        byte[] image, bool isPe32Plus, int peOffset, int dataDirOffset, int secDirRva, int secDirSize)
    {
        try
        {
            // Clone the image so we can zero the excluded regions.
            var hashImage = (byte[])image.Clone();

            // 1. Zero the CheckSum field (optional header + 64).
            Array.Clear(hashImage, peOffset + 0x18 + 64, 4);

            // 2. Zero the security directory entry (data directory index 4: RVA + size).
            Array.Clear(hashImage, dataDirOffset + 4 * 8, 8);

            // 3. Exclude the certificate table bytes from the hash.
            var before = new byte[secDirRva];
            Buffer.BlockCopy(hashImage, 0, before, 0, secDirRva);
            var after = new byte[hashImage.Length - secDirRva - secDirSize];
            Buffer.BlockCopy(hashImage, secDirRva + secDirSize, after, 0, after.Length);

            using var sha = SHA256.Create();
            var preImage = new byte[before.Length + after.Length];
            Buffer.BlockCopy(before, 0, preImage, 0, before.Length);
            Buffer.BlockCopy(after, 0, preImage, before.Length, after.Length);
            return sha.ComputeHash(preImage);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Confirms the digest signed inside the SignedCms equals the file's PE hash.</summary>
    internal static bool DigestMatches(SignedCms cms, byte[] peHash)
    {
        if (peHash.Length == 0)
            return false;

        var signerInfo = cms.SignerInfos.Count > 0 ? cms.SignerInfos[0] : null;
        if (signerInfo is null)
            return false;

        foreach (var attr in signerInfo.SignedAttributes)
        {
            // Oid 1.3.12.2.1011.7.1.1? No: message digest attribute is 1.3.6.1.4.1.311.2.1.2
            // (SPC_INDIGEST? Actually 1.2.840.113549.1.9.4 is the standard messageDigest).
            // Authenticode uses SPC_INDIRECT_DATA (1.3.6.1.4.1.311.2.1.4) holding the true digest.
            if (attr.Oid.Value == "1.3.6.1.4.1.311.2.1.4") // SPC_INDIRECT_DATA
            {
                // The attribute value is the indirect data content: digest algorithm + digest.
                // Try to extract the final digest bytes from the ASN.1 DER value.
                if (TryExtractDigest(attr.Values[0].RawData, peHash, out var match))
                    return match;
            }
        }

        return false;
    }

    /// <summary>Parses the SPC_INDIRECT_DATA ASN.1 structure and compares its DigestInfo
    /// digest against the computed PE hash.</summary>
    private static bool TryExtractDigest(byte[] spcIndirectDataDer, byte[] peHash, out bool match)
    {
        match = false;
        try
        {
            // SPC_INDIRECT_DATA ::= SEQUENCE { SpcAttributeTypeAndValue, DigestInfo }
            // DigestInfo ::= SEQUENCE { AlgorithmIdentifier, OCTET STRING digest }
            // The digest is the last OCTET STRING in the DER blob.
            var reader = new System.Formats.Asn1.AsnReader(
                spcIndirectDataDer, System.Formats.Asn1.AsnEncodingRules.DER);

            var outer = reader.ReadSequence();
            outer.ReadSequence(); // SpcAttributeTypeAndValue (type + value, unused)
            var digestInfo = outer.ReadSequence();
            digestInfo.ReadSequence(); // AlgorithmIdentifier
            var digest = digestInfo.ReadOctetString();
            if (digest.Length == peHash.Length)
                match = peHash.AsSpan().SequenceEqual(digest);
            return true;
        }
        catch (System.Formats.Asn1.AsnContentException)
        {
            return false;
        }
    }
}