namespace Daraban.Platform.Contracts.Assets;

public sealed record AssetCreatedEvent(Guid AssetId, Guid EntityId, string AssetType);

public sealed record AssetUpdatedEvent(Guid AssetId, Guid EntityId, string AssetType);

public sealed record AssetLifecycleChangedEvent(
    Guid AssetId,
    Guid EntityId,
    string FromStatus,
    string ToStatus,
    Guid ActorUserId,
    string? Reason);
