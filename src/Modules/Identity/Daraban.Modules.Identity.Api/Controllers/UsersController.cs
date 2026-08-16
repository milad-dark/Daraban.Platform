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
        return Ok(result.Value);
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
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }
}
