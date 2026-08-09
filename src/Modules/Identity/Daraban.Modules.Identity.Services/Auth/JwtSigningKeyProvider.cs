using System.Security.Cryptography;
using Daraban.Platform.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daraban.Modules.Identity.Services.Auth;

/// <summary>
/// Single source of truth for the RSA key used to both sign (JwtTokenService) and validate
/// (Host.Api's JwtBearer configuration) access tokens -- registered as a singleton so both
/// sides of the same process resolve the exact same key instance.
///
/// SECURITY: outside Development, this throws at first use if Jwt:SigningKeyPemPath isn't
/// configured, rather than silently generating a key nobody can reproduce (which would make
/// every token unverifiable after a restart/second replica, and -- worse -- would be an easy
/// way to accidentally ship an app that "works" in a demo with a throwaway key nobody
/// actually secured). Fail fast beats fail open here.
/// </summary>
public sealed class JwtSigningKeyProvider : IDisposable
{
    private readonly JwtOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<JwtSigningKeyProvider> _logger;
    private readonly object _lock = new();
    private RSA? _key;

    public JwtSigningKeyProvider(IOptions<JwtOptions> options, IHostEnvironment environment, ILogger<JwtSigningKeyProvider> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public RSA GetKey()
    {
        if (_key is not null) return _key;

        lock (_lock)
        {
            if (_key is not null) return _key;

            if (!string.IsNullOrWhiteSpace(_options.SigningKeyPemPath))
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(File.ReadAllText(_options.SigningKeyPemPath));
                _key = rsa;
                return _key;
            }

            if (!_environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKeyPemPath is not configured. A persistent RSA signing key is " +
                    "required outside Development -- refusing to start with an unreproducible " +
                    "ephemeral key. Generate one (e.g. `openssl genrsa -out jwt-signing-key.pem 3072`), " +
                    "keep it out of source control, and set Jwt:SigningKeyPemPath (or the " +
                    "DARABAN_Jwt__SigningKeyPemPath environment variable) to its path.");
            }

            _logger.LogWarning(
                "No Jwt:SigningKeyPemPath configured -- generating an EPHEMERAL development-only " +
                "RSA key. Tokens issued this run will fail validation after a restart or against " +
                "any other process. This must never happen outside Development.");
            _key = RSA.Create(3072);
            return _key;
        }
    }

    public void Dispose() => _key?.Dispose();
}
