using Daraban.Modules.Identity.Services.Users;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Identity.Api.Controllers;

[ApiController]
[Route("api/v1/identity/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    [RequirePermission("identity.users.read")] // Task 2.4 -- first real usage of the dynamic permission policy
    public async Task<IActionResult> Search(
        [FromQuery] Guid? entityId, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await _userService.SearchAsync(entityId, q, page, pageSize, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("identity.users.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _userService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext); // Task 2.2 -- shared mapper, not a per-controller helper
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("identity.users.write")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.CreateAsync(request, ct);

        // A duplicate username/email returns 409 here. This previously read result.Value
        // unconditionally, and Result<T>.Value throws on a failed result -- so a conflict surfaced
        // as an unhandled InvalidOperationException (a 500) instead of the intended 409.
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("identity.users.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.UpdateAsync(id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>
    /// Enable or disable an account. Separate from PUT because disabling revokes every live access
    /// token for that user, which is a materially different act from editing their display name.
    /// </summary>
    [HttpPost("{id:guid}/active")]
    [RequirePermission("identity.users.write")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetUserActiveRequest request, CancellationToken ct)
    {
        var result = await _userService.SetActiveAsync(id, request.IsActive, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("identity.users.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _userService.DeleteAsync(id, ct);
        return result.IsSuccess ? NoContent() : result.Error!.ToProblemResult(HttpContext);
    }
}

public sealed record SetUserActiveRequest(bool IsActive);
