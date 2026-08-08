namespace Daraban.Modules.Identity.Services.Auth;

public interface IRefreshTokenService
{
    /// <summary>Issues a brand-new refresh token (new family) at login.</summary>
    Task<string> IssueAsync(Guid userId, string? issuedFromIp, string? issuedFromUserAgent, CancellationToken ct = default);

    /// <summary>Validates a presented raw refresh token and, if valid, rotates it: the
    /// presented token is revoked, a new one in the same family is issued and returned.
    /// Returns null if the token is invalid/expired/already-used -- a reuse of an
    /// already-rotated token additionally revokes the entire family server-side
    /// (Task 1.3 SS3) before returning null, so the caller should always treat null as
    /// "re-authenticate", never retry.</summary>
    Task<(Guid UserId, string NewToken)?> ValidateAndRotateAsync(string presentedToken, CancellationToken ct = default);

    Task RevokeAsync(string presentedToken, CancellationToken ct = default);
}
