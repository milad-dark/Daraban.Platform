using Daraban.Modules.Identity.Services.Users;
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
    // [RequirePermission("identity.users.read")] -- policy wired once auth (Task 2.x) lands
    public async Task<IActionResult> Search(
        [FromQuery] Guid? entityId, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await _userService.SearchAsync(entityId, q, page, pageSize, ct);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _userService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return NotFound(ProblemFrom(result.Error!));
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var result = await _userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    // Maps Common.Error -> RFC 7807 ProblemDetails (Task 1.4 SS6). A shared
    // ProblemDetailsFactory/exception-mapping middleware replaces this per-controller
    // helper once more than one controller needs it -- left inline here as the seed.
    private static ProblemDetails ProblemFrom(Daraban.Platform.Common.Error error) => new()
    {
        Title = error.Message,
        Status = error.Type == Daraban.Platform.Common.ErrorType.NotFound ? 404 : 400,
        Extensions = { ["errorCode"] = error.Code }
    };
}
