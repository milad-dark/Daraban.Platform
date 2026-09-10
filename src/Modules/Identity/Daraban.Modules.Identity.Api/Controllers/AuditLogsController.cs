using Daraban.Modules.Identity.Services.Audit;
using Daraban.Platform.Hosting;
using Daraban.Platform.Hosting.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Modules.Identity.Api.Controllers;

/// <summary>
/// Read-only audit trail endpoints (Task 7.3). Both actions are pure reads over the
/// append-only audit table; permission <c>identity.auditlogs.read</c> gates access and the
/// platform's dynamic permission policy resolves it.
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService) => _auditLogService = auditLogService;

    /// <summary>Paged, filtered audit trail (entity / actor / action / date range).</summary>
    [HttpGet]
    [RequirePermission("identity.auditlogs.read")]
    public async Task<IActionResult> Search(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid? actorUserId,
        [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _auditLogService.SearchAsync(
            entityType, entityId, actorUserId, action, from, to, page, pageSize, ct);

        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }

    /// <summary>Change history for one record -- backs the inline EntityHistoryComponent.</summary>
    [HttpGet("{entityType}/{entityId:guid}")]
    [RequirePermission("identity.auditlogs.read")]
    public async Task<IActionResult> GetEntityHistory(
        string entityType,
        Guid entityId,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var result = await _auditLogService.GetEntityHistoryAsync(entityType, entityId, limit, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error!.ToProblemResult(HttpContext);
    }
}
