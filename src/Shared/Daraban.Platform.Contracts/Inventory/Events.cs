namespace Daraban.Platform.Contracts.Inventory;

public sealed record RawInventoryReceivedEvent(Guid SubmissionId, Guid AgentId, bool IsPartial);
