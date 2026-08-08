using Daraban.Modules.Identity.Services.Auth;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Daraban.Modules.Identity.Api.Controllers;

/// <summary>
/// Task 2.3. Direct JWT issuance via plain REST endpoints -- NOT the full OpenIddict
/// Authorization Code + PKCE redirect flow originally sketched in Task 1.3. That flow needs
/// a server-hosted login *page* the browser redirects to, which is a different shape of
/// work than "POST /login returns tokens"; this delivers the same JWT-claim design and
/// refresh-token-rotation design from Task 1.3, over a JSON API instead of the OAuth2
/// protocol surface. Revisit as a distinct task if third-party/external OAuth2 clients ever
/// need to authenticate against this server -- first-party Angular SPA + own backend doesn't
/// require it.
///
/// Rate limiting: every action here carries [EnableRateLimiting("auth")] -- policy defined
/// in Host.Api's Program.cs. These endpoints are the classic brute-force/credential-stuffing
/// target; per-IP request throttling here is a second layer behind AuthService's own
/// per-account lockout, not a replacement for it (an attacker spreading attempts across many
/// accounts isn't slowed by account-level lockout alone).
/// </summary>
[ApiController]
[Route("api/v1/identity/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "daraban_rt";

    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService authService, IWebHostEnvironment environment)
    {
        _authService = authService;
        _environment = environment;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);
        return CreatedAtAction(nameof(Register), result.Value);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authService.LoginAsync(request, ip, userAgent, ct);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        SetRefreshCookie(result.Value.RefreshToken);
        return Ok(new { accessToken = result.Value.AccessToken, expiresAt = result.Value.AccessTokenExpiresAt, user = result.Value.User });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var presented) || string.IsNullOrEmpty(presented))
            return ProblemFrom(new Error("IDENTITY.REFRESH_TOKEN_MISSING", "No refresh session found.", ErrorType.Forbidden));

        var result = await _authService.RefreshAsync(presented, ct);
        if (!result.IsSuccess)
        {
            // Always clear the cookie on a failed refresh -- an invalid/reused/expired
            // token should never be presented again by the browser on its own.
            Response.Cookies.Delete(RefreshCookieName);
            return ProblemFrom(result.Error!);
        }

        SetRefreshCookie(result.Value.RefreshToken);
        return Ok(new { accessToken = result.Value.AccessToken, expiresAt = result.Value.AccessTokenExpiresAt, user = result.Value.User });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(RefreshCookieName, out var presented) && !string.IsNullOrEmpty(presented))
            await _authService.LogoutAsync(presented, ct);

        Response.Cookies.Delete(RefreshCookieName);
        return NoContent();
    }

    private void SetRefreshCookie(string refreshToken)
    {
        // HttpOnly: JavaScript can never read this, even via an XSS bug.
        // Secure: never sent over plain HTTP (only disabled for local http-only dev).
        // SameSite=Strict: the browser will not attach this cookie to any cross-site
        // request at all -- the primary CSRF defense for these endpoints. (Origin-header
        // validation as additional defense-in-depth is a documented follow-up, not yet
        // implemented -- SameSite=Strict alone already blocks the standard CSRF attack shape
        // in every current browser.)
        // Path scoped to the auth routes only, so this cookie is never sent on ordinary API
        // calls that don't need it.
        Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment() || Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/identity/auth",
            Expires = DateTimeOffset.UtcNow.AddDays(90), // absolute cap; server-side rotation/expiry is the real enforcement
        });
    }

    private ObjectResult ProblemFrom(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status400BadRequest,
        };
        return new ObjectResult(new ProblemDetails
        {
            Title = error.Message,
            Status = status,
            Extensions = { ["errorCode"] = error.Code },
        })
        { StatusCode = status };
    }
}
