using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Host.AgentApi.Controllers;

[ApiController]
[Route("api/v1/agents")]
[Authorize]
public class AgentManagementController(IAgentService agentService) : ControllerBase
{

    // ---- Agent CRUD ----

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] RegisterAgentRequest request, CancellationToken ct)
    {
        // TODO: Extract owner user ID from the authenticated user's JWT (admin endpoint)
        // For now, use a placeholder — this will be wired when the admin auth pipeline is added
        var ownerUserId = GetActorUserId();

        var result = await agentService.RegisterAsync(request, ownerUserId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await agentService.GetByIdAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] string? status, [FromQuery] string? type,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        AgentStatus? statusEnum = status?.ToLowerInvariant() switch
        {
            "active" => AgentStatus.Active,
            "suspended" => AgentStatus.Suspended,
            "deactivated" => AgentStatus.Deactivated,
            _ => null,
        };

        AgentType? typeEnum = type?.ToLowerInvariant() switch
        {
            "inventoryscanner" => AgentType.InventoryScanner,
            "assetmonitor" => AgentType.AssetMonitor,
            "servicedeskbot" => AgentType.ServiceDeskBot,
            "integrationconnector" => AgentType.IntegrationConnector,
            _ => null,
        };

        var result = await agentService.GetPagedAsync(statusEnum, typeEnum, search, page, pageSize, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAgentRequest request, CancellationToken ct)
    {
        var result = await agentService.UpdateAsync(id, request, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        var result = await agentService.DeactivateAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return NoContent();
    }

    // ---- Credential Management ----

    [HttpPost("{id:guid}/credentials")]
    public async Task<IActionResult> CreateCredential(Guid id, [FromBody] CreateCredentialRequest request, CancellationToken ct)
    {
        var result = await agentService.CreateCredentialAsync(id, request, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        // CredentialCreatedResponse contains the plain-text secret — returned ONCE
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}/credentials")]
    public async Task<IActionResult> GetCredentials(Guid id, CancellationToken ct)
    {
        var result = await agentService.GetCredentialsAsync(id, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}/credentials/{credentialId:guid}")]
    public async Task<IActionResult> RevokeCredential(Guid id, Guid credentialId, CancellationToken ct)
    {
        var result = await agentService.RevokeCredentialAsync(id, credentialId, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return NoContent();
    }

    // ---- Audit Log ----

    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> GetAuditLog(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await agentService.GetAuditLogAsync(id, page, pageSize, ct);
        if (!result.IsSuccess)
            return ProblemFrom(result.Error!);
        return Ok(result.Value);
    }

    // ---- Helpers ----

    /// <summary>
    /// Extracts the actor user ID from the JWT. For agent management endpoints,
    /// this is the human user performing the action (admin).
    /// TODO: Replace with proper ICurrentUser resolution once the admin auth pipeline is wired.
    /// </summary>
    private Guid GetActorUserId()
    {
        var sub = User.FindFirst("sub")?.Value;
        return sub is not null ? Guid.Parse(sub) : Guid.Empty;
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
