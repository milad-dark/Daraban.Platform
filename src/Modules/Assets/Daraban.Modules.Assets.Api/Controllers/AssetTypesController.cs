using Daraban.Modules.Assets.Services.Dtos;
using Daraban.Modules.Assets.Services.Interfaces;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Assets.Api.Controllers;

[ApiController]
[Route("api/v1/asset-types")]
[Authorize]
public class AssetTypesController : ControllerBase
{
    private readonly IAssetTypeService _assetTypeService;

    public AssetTypesController(IAssetTypeService assetTypeService) => _assetTypeService = assetTypeService;

    [HttpGet]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _assetTypeService.GetAllAsync(ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("assets.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _assetTypeService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPost]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Create([FromBody] CreateAssetTypeRequest request, CancellationToken ct)
    {
        var result = await _assetTypeService.CreateAsync(request, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("assets.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateAssetTypeRequest request, CancellationToken ct)
    {
        var result = await _assetTypeService.UpdateAsync(id, request, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("assets.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _assetTypeService.DeleteAsync(id, ct);
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
