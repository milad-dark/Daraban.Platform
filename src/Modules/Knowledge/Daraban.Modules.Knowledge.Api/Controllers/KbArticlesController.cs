using Daraban.Modules.Knowledge.Data.Entities;
using Daraban.Modules.Knowledge.Services.Dtos;
using Daraban.Modules.Knowledge.Services.Interfaces;
using Daraban.Platform.Abstractions;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Knowledge.Api.Controllers;

/// <summary>
/// GET/POST/PUT/DELETE /api/v1/kb/articles, plus search and feedback (Task 6.4).
/// </summary>
[ApiController]
[Route("api/v1/kb/articles")]
[Authorize]
public class KbArticlesController : ControllerBase
{
    private readonly IKbArticleService _articles;
    private readonly ICurrentUser _currentUser;

    public KbArticlesController(IKbArticleService articles, ICurrentUser currentUser)
    {
        _articles = articles;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission("knowledge.read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? categoryId,
        [FromQuery] KbArticleStatus? status,
        [FromQuery] bool? isFaq,
        [FromQuery] Guid? authorUserId,
        [FromQuery] string? title,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _articles.GetPagedAsync(
            _currentUser.ActiveEntityId, categoryId, status, isFaq, authorUserId, title, page, pageSize, ct);

        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>
    /// Full-text search over title+content via the generated tsvector column, ranked by
    /// ts_rank (Task 6.4: PostgreSQL only, no Elasticsearch).
    /// Route sits above {id:guid} in this file for readability -- ASP.NET Core matches the
    /// literal "search" segment over the guid-constrained parameter regardless of order.
    /// </summary>
    [HttpGet("search")]
    [RequirePermission("knowledge.read")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] Guid? categoryId,
        [FromQuery] KbArticleStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _articles.SearchAsync(
            _currentUser.ActiveEntityId, q, categoryId, status, page, pageSize, ct);

        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Reads one article. Pass countView=true from the reader UI to increment the view
    /// counter; the editor should leave it false so editing doesn't inflate the count.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("knowledge.read")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] bool countView = false, CancellationToken ct = default)
    {
        var result = await _articles.GetByIdAsync(id, countView, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpPost]
    [RequirePermission("knowledge.write")]
    public async Task<IActionResult> Create([FromBody] CreateKbArticleRequest request, CancellationToken ct)
    {
        var result = await _articles.CreateAsync(
            request, _currentUser.ActiveEntityId, _currentUser.UserId, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("knowledge.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateKbArticleRequest request, CancellationToken ct)
    {
        var result = await _articles.UpdateAsync(id, request, _currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>
    /// Draft / Published / Archived transition. Separate from PUT so publishing is an explicit
    /// act with its own permission surface, not a field flipped inside a content edit.
    /// </summary>
    [HttpPost("{id:guid}/status")]
    [RequirePermission("knowledge.publish")]
    public async Task<IActionResult> ChangeStatus(
        Guid id, [FromBody] ChangeKbArticleStatusRequest request, CancellationToken ct)
    {
        var result = await _articles.ChangeStatusAsync(id, request.Status, _currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("knowledge.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _articles.DeleteAsync(id, _currentUser.UserId, ct);
        return result.IsSuccess ? NoContent() : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>
    /// Helpful / not-helpful verdict. Guarded by knowledge.read, not knowledge.write: any
    /// reader who can see an article is allowed to rate it.
    /// </summary>
    [HttpPost("{id:guid}/feedback")]
    [RequirePermission("knowledge.read")]
    public async Task<IActionResult> SubmitFeedback(
        Guid id, [FromBody] SubmitKbFeedbackRequest request, CancellationToken ct)
    {
        var result = await _articles.SubmitFeedbackAsync(id, request, _currentUser.UserId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>All feedback for an article, including comments -- author/editor view.</summary>
    [HttpGet("{id:guid}/feedback")]
    [RequirePermission("knowledge.write")]
    public async Task<IActionResult> GetFeedback(Guid id, CancellationToken ct)
    {
        var result = await _articles.GetFeedbackAsync(id, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }
}
