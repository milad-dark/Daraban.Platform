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
public class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgetService;
    private readonly ICurrentUser _currentUser;

    public BudgetsController(IBudgetService budgetService, ICurrentUser currentUser)
    {
        _budgetService = budgetService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid entityNodeId,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _budgetService.GetPagedAsync(entityNodeId, search, isActive, page, pageSize, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _budgetService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBudgetRequest request, CancellationToken ct)
    {
        var result = await _budgetService.CreateAsync(request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBudgetRequest request, CancellationToken ct)
    {
        var result = await _budgetService.UpdateAsync(id, request, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _budgetService.DeleteAsync(id, _currentUser.UserId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return NoContent();
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] Guid entityNodeId, CancellationToken ct)
    {
        var result = await _budgetService.GetSummaryAsync(entityNodeId, ct);
        if (!result.IsSuccess)
            return result.Error!.ToProblemResult(HttpContext);

        return Ok(result.Value);
    }

}
