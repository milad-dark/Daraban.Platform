using Daraban.Modules.Identity.Data.Entities;
using Daraban.Modules.Identity.Data.Repositories;
using Daraban.Modules.Identity.Services.Agents;
using Daraban.Modules.Inventory.Data.Repositories;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Host.Api.Controllers;

/// <summary>
/// Admin-facing agent dashboard endpoints (Task 4.5). These power the Angular UI:
///   - Agent list with online/offline status
///   - Agent detail with tabs (Info, Inventory, Commands, Logs)
///   - Fleet overview (summary cards)
///   - Command dispatch and history
///
/// Authentication: valid user JWT (admin panel). NOT agent tokens.
/// </summary>
[ApiController]
[Route("api/v1/agents")]
[Authorize]
public class AdminAgentController(
    IAgentService agentService,
    IAgentCommandRepository commandRepository,
    IInventoryRepository inventoryRepository) : ControllerBase
{
    private const int MaxPageSize = 100;

    // ---- List ----

    /// <summary>
    /// List agents with status badges for the admin table.
    /// GET /api/v1/agents?status=active&type=inventoryscanner&search=scanner&page=1&pageSize=20
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAgents(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, MaxPageSize);

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

        var items = await agentService.GetAgentListAsync(statusEnum, typeEnum, search, page, pageSize, ct);
        var total = await agentService.GetAgentListCountAsync(statusEnum, typeEnum, search, ct);

        // Batch-fetch latest inventory snapshots (1 query, not N)
        var agentIds = items.Select(a => a.Id).ToList();
        var snapshots = await inventoryRepository.GetLatestByAgentIdsAsync(agentIds, ct);

        // Enrich with inventory hostname/OS data
        var enriched = new List<AgentListItemDto>(items.Count);
        foreach (var item in items)
        {
            snapshots.TryGetValue(item.Id, out var snapshot);
            enriched.Add(new AgentListItemDto(
                item.Id, item.Name, item.Description, item.Type, item.Status,
                Hostname: snapshot?.DeviceId,
                OperatingSystem: ExtractOperatingSystem(snapshot?.RawPayload),
                item.LastActiveAt,
                item.IsOnline,
                item.PendingCommandCount,
                item.TotalCommandCount,
                item.CreatedAt));
        }

        return Ok(new { items = enriched, totalCount = total, page, pageSize });
    }

    // ---- Fleet Summary ----

    /// <summary>
    /// Aggregate fleet status for dashboard overview cards.
    /// GET /api/v1/agents/summary
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await agentService.GetFleetSummaryAsync(ct);
        return Ok(summary);
    }

    // ---- Detail ----

    /// <summary>
    /// Full agent detail with command stats and inventory status.
    /// GET /api/v1/agents/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var detail = await agentService.GetAgentDetailAsync(id, ct);
        if (detail is null)
            return NotFound(new ProblemDetails { Title = "Agent not found.", Status = 404 });

        // Enrich with inventory snapshot data
        var snapshot = await inventoryRepository.GetLatestByAgentIdAsync(id, ct);
        if (snapshot is not null)
        {
            detail = detail with
            {
                LastInventoryAt = snapshot.ReceivedAt,
                LastInventoryStatus = snapshot.Status.ToString(),
            };
        }

        return Ok(detail);
    }

    // ---- Command History ----

    /// <summary>
    /// Command history for an agent (tab on detail view).
    /// GET /api/v1/agents/{id}/jobs?page=1&pageSize=20
    /// </summary>
    [HttpGet("{id:guid}/jobs")]
    public async Task<IActionResult> GetJobs(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        pageSize = Math.Min(pageSize, MaxPageSize);

        var agent = await agentService.GetByIdAsync(id, ct);
        if (!agent.IsSuccess)
            return NotFound(new ProblemDetails { Title = "Agent not found.", Status = 404 });

        var entries = await agentService.GetCommandHistoryAsync(id, page, pageSize, ct);
        var total = await commandRepository.GetCommandCountByAgentAsync(id, ct);

        return Ok(new { items = entries, totalCount = total, page, pageSize });
    }

    // ---- Inventory Snapshot ----

    /// <summary>
    /// Latest inventory snapshot for an agent (tab on detail view).
    /// GET /api/v1/agents/{id}/inventory
    /// </summary>
    [HttpGet("{id:guid}/inventory")]
    public async Task<IActionResult> GetInventory(Guid id, CancellationToken ct)
    {
        var agent = await agentService.GetByIdAsync(id, ct);
        if (!agent.IsSuccess)
            return NotFound(new ProblemDetails { Title = "Agent not found.", Status = 404 });

        var snapshot = await inventoryRepository.GetLatestByAgentIdAsync(id, ct);
        if (snapshot is null)
            return Ok((object?)null);

        return Ok(new AgentInventorySnapshotDto(
            snapshot.Id,
            snapshot.DeviceId,
            snapshot.ItemType,
            snapshot.Action,
            snapshot.Status.ToString(),
            snapshot.DeviceCount,
            snapshot.SubmittedAt,
            snapshot.ReceivedAt,
            snapshot.ProcessedAt));
    }

    // ---- Helpers ----

    /// <summary>
    /// Best-effort extraction of OS from the raw inventory JSON payload.
    /// The Agent's DarabanClient sends device info with a "systemInfo" field
    /// containing osName/osVersion. Returns null if not parseable.
    /// </summary>
    private static string? ExtractOperatingSystem(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
            return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            // Try systemInfo.osName (common Agent inventory shape)
            if (root.TryGetProperty("systemInfo", out var sysInfo) &&
                sysInfo.TryGetProperty("osName", out var osName))
            {
                var version = sysInfo.TryGetProperty("osVersion", out var osVer)
                    ? osVer.GetString() : null;
                return version is not null ? $"{osName.GetString()} {version}" : osName.GetString();
            }

            // Try os property (alternative shape)
            if (root.TryGetProperty("os", out var os))
                return os.GetString();
        }
        catch
        {
            // Non-JSON or malformed — not an error, just return null
        }

        return null;
    }
}
