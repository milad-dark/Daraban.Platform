namespace Daraban.Modules.Discovery.Data.Entities;

/// <summary>
/// Rules for automatically creating Assets from discovered devices (Task 5.1).
/// When a device matches the rule criteria, an Asset is created automatically.
/// </summary>
public class DiscoveryRule
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable rule name (e.g., "Create Windows Servers").</summary>
    public string Name { get; set; } = default!;

    /// <summary>Rule description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this rule is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Priority order (lower = higher priority). Multiple rules can match.</summary>
    public int Priority { get; set; } = 0;

    /// <summary>Filter criteria as JSON object (supports: OsGuess, Vendor, Model, OpenPorts, etc.).</summary>
    public string FilterCriteria { get; set; } = "{}";

    /// <summary>What to do when a device matches: CreateAsset, UpdateAsset, Ignore.</summary>
    public MatchAction Action { get; set; } = MatchAction.CreateAsset;

    /// <summary>Asset type to create (e.g., "Server", "Workstation", "NetworkDevice").</summary>
    public string? AssetType { get; set; }

    /// <summary>Entity ID to assign the created asset to (null = unassigned).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Tag to add to created assets (e.g., "auto-discovered").</summary>
    public string? Tag { get; set; }

    /// <summary>Whether to notify admin when a new asset is created.</summary>
    public bool NotifyOnCreate { get; set; } = false;

    /// <summary>How many assets this rule has created (denormalized counter).</summary>
    public int AssetsCreatedCount { get; set; } = 0;

    /// <summary>When this rule was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this rule was last modified.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>User who created this rule.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>When this rule was last executed (null if never).</summary>
    public DateTimeOffset? LastExecutedAt { get; set; }

    /// <summary>When this rule was last matched (null if never).</summary>
    public DateTimeOffset? LastMatchedAt { get; set; }
}

/// <summary>Match action enumeration.</summary>
public enum MatchAction
{
    /// <summary>Create a new Asset from the discovered device.</summary>
    CreateAsset = 0,

    /// <summary>Update existing Asset with new discovery data.</summary>
    UpdateAsset = 1,

    /// <summary>Ignore the device (don't create/update Asset).</summary>
    Ignore = 2,

    /// <summary>Create asset and add to specific entity.</summary>
    CreateAndAssign = 3,
}
