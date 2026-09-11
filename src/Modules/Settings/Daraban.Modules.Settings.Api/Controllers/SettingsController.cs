using Daraban.Modules.Settings.Services;
using Daraban.Modules.Settings.Services.Dtos;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Settings.Api.Controllers;

/// <summary>
/// Platform settings endpoints (Task 7.4). The controller is a thin adapter: parse the
/// route, call the service, map Result to HTTP. Reads need <c>settings.read</c>; writes and
/// connectivity tests need <c>settings.write</c> -- resolved by the platform's dynamic
/// permission policy. Secret values are masked by the service before any DTO exists, so no
/// controller path can ever leak one (defense in depth: the service is the single owner).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/settings")]
public sealed class SettingsController(
    ISettingsService settingsService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>All settings grouped by category, in UI tab order. Secrets masked.</summary>
    /// <remarks>GET /api/v1/settings</remarks>
    [HttpGet]
    [RequirePermission("settings.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await settingsService.GetAllAsync(ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Updates one setting's value. The actor always comes from the JWT, never
    /// from the request body (OWASP A01 -- no IDOR surface).</summary>
    /// <remarks>PUT /api/v1/settings/{key}</remarks>
    [HttpPut("{key}")]
    [RequirePermission("settings.write")]
    public async Task<IActionResult> Update(string key, [FromBody] UpdateSettingRequest request, CancellationToken ct)
    {
        var result = await settingsService.UpdateAsync(key, request, currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Connectivity test for the category owning the key (email SMTP or LDAP).
    /// Disabled accounts cannot pass; the handler checks the caller's permission set.</summary>
    /// <remarks>POST /api/v1/settings/test/{key}</remarks>
    [HttpPost("test/{key}")]
    [RequirePermission("settings.write")]
    public async Task<IActionResult> TestConnection(string key, CancellationToken ct)
    {
        var result = await settingsService.TestConnectionAsync(key, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }
}
