using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Modules.Knowledge.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Knowledge.Api.Controllers;

/// <summary>
/// GET/POST/PUT/DELETE /api/v1/kb/categories (Task 6.4).
/// </summary>
[ApiController]
[Route("api/v1/kb/categories")]
[Authorize]
public class KbCategoriesController : ControllerBase
{
    private readonly IKbCategoryService _categories;
    private readonly ICurrentUser _currentUser;

    public KbCategoriesController(IKbCategoryService categories, ICurrentUser currentUser)
    {
        _categories = categories;
        _currentUser = currentUser;
    }

    /// <summary>Flat list by default; pass tree=true for the nested form.</summary>
    [HttpGet]
    [RequirePermission("knowledge.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool tree = false,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        if (tree)
        {
            var treeResult = await _categories.GetTreeAsync(_currentUser.ActiveEntityId, includeInactive, ct);
            return treeResult.IsSuccess
                ? Ok(treeResult.Value)
                : treeResult.Error!.ToProblemResult(HttpContext);
        }

        var result = await _categories.GetAllAsync(_currentUser.ActiveEntityId, includeInactive, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("knowledge.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _categories.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpPost]
    [RequirePermission("knowledge.write")]
    public async Task<IActionResult> Create([FromBody] CreateKbCategoryRequest request, CancellationToken ct)
    {
        var result = await _categories.CreateAsync(
            request, _currentUser.ActiveEntityId, _currentUser.UserId, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("knowledge.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKbCategoryRequest request, CancellationToken ct)
    {
        var result = await _categories.UpdateAsync(id, request, _currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("knowledge.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _categories.DeleteAsync(id, _currentUser.UserId, ct);
        return result.IsSuccess ? NoContent() : result.Error!.ToProblemResult(HttpContext);
    }
}
