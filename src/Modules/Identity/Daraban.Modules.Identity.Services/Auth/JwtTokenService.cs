using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Daraban.Modules.Identity.Data.Entities;
using Daraban.Platform.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Daraban.Modules.Identity.Services.Auth;

/// <summary>
/// RS256 (asymmetric) signing per Task 1.3 SS2 -- no shared secret between the process that
/// signs and any process that only needs to validate. Deliberately does NOT embed the full
/// per-entity-per-profile rights matrix as claims (Task 1.3 SS2.1) -- permissions are
/// resolved server-side per request (IPermissionResolver, not yet implemented) so a rights
/// change takes effect immediately instead of waiting for token expiry.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly JwtSigningKeyProvider _keyProvider;

    public JwtTokenService(IOptions<JwtOptions> options, JwtSigningKeyProvider keyProvider)
    {
        _options = options.Value;
        _keyProvider = keyProvider;
    }

    public (string Token, DateTimeOffset ExpiresAt) IssueAccessToken(User user, Guid activeEntityId)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("active_entity_id", activeEntityId.ToString()),
            new Claim("name", user.Username),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            // Compared against identity.users.token_version on every request (Task 1.3 SS8)
            // so a still-unexpired token can be revoked immediately (password change, forced
            // logout, admin action) without needing a token denylist.
            new Claim("token_version", user.TokenVersion.ToString()),
        };

        var credentials = new SigningCredentials(new RsaSecurityKey(_keyProvider.GetKey()), SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
