using Daraban.Modules.Plugins.Services;
using Daraban.Modules.Plugins.Services.Dtos;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http; // IFormFile
using System.Net.Mime;

namespace Daraban.Modules.Plugins.Api.Controllers;

/// <summary>
/// Admin surface for the plugin registry (Task 7.5). A thin adapter: auth via
/// RequirePermission, actor from the JWT, and every business outcome mapped
/// through the Result pattern -- no logic lives here.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/plugins")]
public sealed class PluginsController(
    IPluginService pluginService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List every installed plugin (all states, but not tombstones).</summary>
    [HttpGet]
    [RequirePermission("plugins.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var plugins = await pluginService.ListAsync(ct);
        return Ok(plugins.Select(p => p.ToDto()).ToList());
    }

    /// <summary>Fetch one plugin registry row.</summary>
    [HttpGet("{pluginId}")]
    [RequirePermission("plugins.read")]
    public async Task<IActionResult> Get(string pluginId, CancellationToken ct)
    {
        var result = await pluginService.GetAsync(pluginId, ct);
        return result.IsSuccess ? Ok(result.Value!.ToDto()) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Install a plugin package (.zip) as multipart/form-data. Admin only.</summary>
    [HttpPost]
    [RequirePermission("plugins.write")]
    [RequestSizeLimit(22_010_753)] // 21 MB: 20 MB package cap + multipart overhead headroom
    [Produces(MediaTypeNames.Application.Json)]
    public async Task<IActionResult> Install(IFormFile package, CancellationToken ct)
    {
        if (package is null || package.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "A plugin package (.zip) file is required.",
                Extensions = { ["code"] = "PLUGINS.PACKAGE_REQUIRED" },
            });
        }

        await using var stream = package.OpenReadStream();
        var result = await pluginService.InstallAsync(stream, currentUser.UserId, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { pluginId = result.Value!.PluginId }, result.Value.ToDto())
            : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Load and enable an installed (disabled/new) plugin.</summary>
    [HttpPost("{pluginId}/enable")]
    [RequirePermission("plugins.write")]
    public async Task<IActionResult> Enable(string pluginId, CancellationToken ct)
    {
        var result = await pluginService.EnableAsync(pluginId, currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value!.ToDto()) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Disable a plugin: unload runtime state, keep package + schema.</summary>
    [HttpPost("{pluginId}/disable")]
    [RequirePermission("plugins.write")]
    public async Task<IActionResult> Disable(string pluginId, CancellationToken ct)
    {
        var result = await pluginService.DisableAsync(pluginId, currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value!.ToDto()) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Uninstall: unload, drop the plugin's isolated schema, delete files,
    /// keep a registry tombstone for audit.</summary>
    [HttpDelete("{pluginId}")]
    [RequirePermission("plugins.write")]
    public async Task<IActionResult> Uninstall(string pluginId, CancellationToken ct)
    {
        var result = await pluginService.UninstallAsync(pluginId, currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value!.ToDto()) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Menu items contributed by enabled plugins (for the Angular shell).
    /// Permission-filtered server-side; entries the user lacks are omitted.</summary>
    [HttpGet("menu-items")]
    [RequirePermission("plugins.read")]
    public IActionResult MenuItems()
    {
        var items = pluginService.GetMenuItems();
        return Ok(items.Select(i => new PluginMenuItemDto(
            i.Title, i.Route, i.Icon, i.Order, i.RequiredPermission)));
    }
}