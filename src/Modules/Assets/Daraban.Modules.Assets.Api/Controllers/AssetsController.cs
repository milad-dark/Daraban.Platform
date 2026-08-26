using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Assets.Api.Controllers;

[ApiController]
[Route("api/v1/assets")]
[Authorize]
public class AssetsController : ControllerBase
{
    private readonly IAssetService _assetService;
    private readonly ICurrentUser _currentUser;

    public AssetsController(IAssetService assetService, ICurrentUser currentUser)
    {
        _assetService = assetService;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] Guid? assetTypeId, [FromQuery] Guid? locationId, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _assetService.GetPagedAsync(
            _currentUser.ActiveEntityId, status, assetTypeId, locationId, search, page, pageSize, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _assetService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Create([FromBody] CreateAssetRequest request, CancellationToken ct)
    {
        var result = await _assetService.CreateAsync(request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAssetRequest request, CancellationToken ct)
    {
        var result = await _assetService.UpdateAsync(id, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("assets.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _assetService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return NoContent();
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
