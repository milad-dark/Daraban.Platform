namespace Daraban.Platform.Contracts.Agents;

/// <summary>
/// Published when a new agent is registered (Task 4.1 SS3).
/// Consumers: Notifications (welcome email to owner), Reporting (agent onboarding metric).
/// </summary>
public sealed record AgentRegisteredEvent(Guid AgentId, string AgentName, string AgentType, Guid? OwnerUserId, Guid? EntityId);

/// <summary>
/// Published when an agent is deactivated (Task 4.1 SS3).
/// Consumers: Inventory (stop accepting submissions), Notifications (alert owner).
/// </summary>
public sealed record AgentDeactivatedEvent(Guid AgentId, string AgentName, DateTimeOffset DeactivatedAt);

/// <summary>
/// Published when an agent credential is revoked (Task 4.1 SS3).
/// Consumers: AgentControlHub (push invalidation to connected agents).
/// </summary>
public sealed record AgentCredentialRevokedEvent(Guid AgentId, Guid CredentialId, string ClientId);

/// <summary>
/// Published when an agent submits a command for async processing (Task 4.1 SS3).
/// Consumed by: RuleEvaluator, InventoryProcessor, or any module that handles agent commands.
/// </summary>
public sealed record AgentCommandPublishedEvent(Guid CommandId, Guid AgentId, string CommandType, string? TargetModule, string? Payload, int? TimeoutSeconds, DateTimeOffset QueuedAt);
