namespace Daraban.Platform.Abstractions;

/// <summary>
/// Binds to the "Jwt" config section (already present in every host's appsettings.json,
/// Task 2.1). Shared between Identity.Services (which signs tokens with the private key)
/// and Host.Api/Host.AgentApi (which validate them with the public key) -- same process for
/// now, so the same PEM works for both; if issuance and validation ever split across
/// processes, only the public key needs distributing.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://localhost:5443";
    public string Audience { get; set; } = "daraban-api";

    /// <summary>Minutes. Kept short per Task 1.3 SS2 -- a leaked access token has a small
    /// blast radius, and token_version (Task 1.3 SS8) covers immediate revocation anyway.</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    /// <summary>Days. Sliding -- see RefreshTokenService.</summary>
    public int RefreshTokenLifetimeDays { get; set; } = 14;
    public int RefreshTokenAbsoluteLifetimeDays { get; set; } = 90;

    /// <summary>Filesystem path to a PEM-encoded RSA private key (PKCS#1 or PKCS#8). Must
    /// NEVER be committed to source control -- see JwtSigningKeyProvider for what happens
    /// when this is left unset (safe in Development, fails startup in every other
    /// environment rather than silently running insecurely).</summary>
    public string? SigningKeyPemPath { get; set; }
}
