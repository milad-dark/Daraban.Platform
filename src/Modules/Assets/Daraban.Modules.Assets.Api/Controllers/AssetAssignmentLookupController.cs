using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Assets.Api.Controllers;

/// <summary>
/// Separate controller for cross-entity queries: "show me all assets assigned to user X".
/// Route does NOT nest under /assets — these are reverse-lookup queries.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class AssetAssignmentLookupController : ControllerBase
{
    private readonly IAssetAssignmentService _assignmentService;

    public AssetAssignmentLookupController(IAssetAssignmentService assignmentService)
        => _assignmentService = assignmentService;

    /// <summary>Get all assets currently assigned to a specific user.</summary>
    [HttpGet("users/{userId:guid}/assets")]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetByUser(Guid userId, CancellationToken ct)
    {
        var result = await _assignmentService.GetByTargetAsync(
            AssignmentTargetType.User, userId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return Ok(result.Value);
    }

    /// <summary>Get all assets currently assigned to a specific department.</summary>
    [HttpGet("departments/{departmentId:guid}/assets")]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetByDepartment(Guid departmentId, CancellationToken ct)
    {
        var result = await _assignmentService.GetByTargetAsync(
            AssignmentTargetType.Department, departmentId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);
        return Ok(result.Value);
    }

}
