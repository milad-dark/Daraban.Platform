using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Data.Entities;

/// <summary>
/// A registered non-human principal (AI agent, automation bot, integration service)
/// that authenticates via OAuth2 client_credentials (Task 4.1 SS1). Agents are NOT users —
/// they have scope-based permissions, not the per-entity-per-profile RBAC model.
/// </summary>
public class Agent : SoftDeletableEntity
{
    /// <summary>Human-readable name (e.g. "Inventory Scanner Agent #3").</summary>
    public string Name { get; set; } = default!;

    /// <summary>Optional description of the agent's purpose.</summary>
    public string? Description { get; set; }

    /// <summary>The user who registered this agent. NULL means system-created.</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>The entity/node this agent operates under. NULL means global (no entity scope).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Agent type determines default capabilities. See <see cref="AgentType"/>.</summary>
    public AgentType Type { get; set; } = AgentType.Generic;

    /// <summary>Current operational status. Disabled agents cannot authenticate.</summary>
    public AgentStatus Status { get; set; } = AgentStatus.Active;

    /// <summary>
    /// Comma-separated scopes this agent is allowed to request (e.g. "inventory:write,assets:read").
    /// The actual token scopes are the intersection of this and what the OAuth2 client requests.
    /// </summary>
    public string AllowedScopes { get; set; } = string.Empty;

    /// <summary>Rate limit: max requests per minute. 0 = unlimited.</summary>
    public int RateLimitPerMinute { get; set; }

    /// <summary>Optional tags for filtering/organizing agents (JSON array stored as text).</summary>
    public string? Tags { get; set; }

    public DateTimeOffset? LastActiveAt { get; set; }

    // ---- Navigation ----
    public ICollection<AgentCredential> Credentials { get; set; } = new List<AgentCredential>();
    public ICollection<AgentAuditLog> AuditLogs { get; set; } = new List<AgentAuditLog>();
}

public enum AgentType
{
    /// <summary>General-purpose agent with no pre-configured capabilities.</summary>
    Generic = 0,

    /// <summary>Inventory submission agent (barcode scanner, IoT device).</summary>
    InventoryScanner = 1,

    /// <summary>Asset monitoring agent (health checks, telemetry collection).</summary>
    AssetMonitor = 2,

    /// <summary>ServiceDesk ticket agent (auto-triage, SLA monitoring).</summary>
    ServiceDeskBot = 3,

    /// <summary>External integration connector (ERP sync, CRM bridge).</summary>
    IntegrationConnector = 4,
}

public enum AgentStatus
{
    /// <summary>Active and can authenticate.</summary>
    Active = 0,

    /// <summary>Temporarily suspended — cannot authenticate, credentials preserved.</summary>
    Suspended = 1,

    /// <summary>Permanently deactivated — cannot authenticate, will be soft-deleted.</summary>
    Deactivated = 2,
}
