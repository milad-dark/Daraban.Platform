using Daraban.Modules.Financial.Services.Dtos;
using Daraban.Modules.Financial.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Common;
using Daraban.Platform.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Financial.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InfocomsController : ControllerBase
{
    private readonly IInfocomService _infocomService;
    private readonly ICurrentUser _currentUser;

    public InfocomsController(IInfocomService infocomService, ICurrentUser currentUser)
    {
        _infocomService = infocomService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid entityNodeId,
        [FromQuery] string? search = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] Guid? budgetId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _infocomService.GetPagedAsync(entityNodeId, search, supplierId, budgetId, page, pageSize, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _infocomService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpGet("asset/{assetId:guid}")]
    public async Task<IActionResult> GetByAssetId(Guid assetId, CancellationToken ct)
    {
        var result = await _infocomService.GetByAssetIdAsync(assetId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInfocomRequest request, CancellationToken ct)
    {
        var result = await _infocomService.CreateAsync(request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateInfocomRequest request, CancellationToken ct)
    {
        var result = await _infocomService.UpdateAsync(id, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _infocomService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return NoContent();
    }

    [HttpGet("{id:guid}/depreciation")]
    public async Task<IActionResult> CalculateDepreciation(Guid id, CancellationToken ct)
    {
        var result = await _infocomService.CalculateDepreciationAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

}
