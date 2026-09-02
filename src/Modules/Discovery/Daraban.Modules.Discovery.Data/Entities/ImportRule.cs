namespace Daraban.Modules.Discovery.Data.Entities;

/// <summary>
/// Import rule for discovered devices (GLPI-style).
/// Defines criteria for matching devices and actions to apply.
/// </summary>
public class ImportRule
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable rule name.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Rule description.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this rule is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Priority order (lower = higher priority).</summary>
    public int Priority { get; set; } = 0;

    /// <summary>When this rule was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this rule was last modified.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>User who created this rule.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Navigation: rule criteria.</summary>
    public ICollection<ImportRuleCriteria> Criteria { get; set; } = new List<ImportRuleCriteria>();

    /// <summary>Navigation: rule actions.</summary>
    public ICollection<ImportRuleAction> Actions { get; set; } = new List<ImportRuleAction>();
}

/// <summary>
/// Criteria for matching discovered devices.
/// </summary>
public class ImportRuleCriteria
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to ImportRule.</summary>
    public Guid ImportRuleId { get; set; }

    /// <summary>Field to match (e.g., "IpAddress", "MacAddress", "Hostname", "OsGuess", "Vendor", "OpenPorts").</summary>
    public string Field { get; set; } = default!;

    /// <summary>Operator (e.g., "Contains", "Equals", "StartsWith", "EndsWith", "Matches", "GreaterThan", "LessThan").</summary>
    public string Operator { get; set; } = default!;

    /// <summary>Value to match against.</summary>
    public string Value { get; set; } = default!;

    /// <summary>Navigation: parent rule.</summary>
    public ImportRule ImportRule { get; set; } = default!;
}

/// <summary>
/// Actions to apply when a device matches the rule criteria.
/// </summary>
public class ImportRuleAction
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Foreign key to ImportRule.</summary>
    public Guid ImportRuleId { get; set; }

    /// <summary>Action type (e.g., "AssignEntity", "AssignLocation", "AssignType", "AssignTag", "Ignore", "CreateAsset").</summary>
    public string ActionType { get; set; } = default!;

    /// <summary>Action value (e.g., entity GUID, location name, device type).</summary>
    public string? Value { get; set; }

    /// <summary>Navigation: parent rule.</summary>
    public ImportRule ImportRule { get; set; } = default!;
}

/// <summary>
/// Known field names for import rule criteria.
/// </summary>
public static class ImportRuleFields
{
    public const string IpAddress = "IpAddress";
    public const string MacAddress = "MacAddress";
    public const string Hostname = "Hostname";
    public const string OsGuess = "OsGuess";
    public const string OsVersion = "OsVersion";
    public const string Vendor = "Vendor";
    public const string Model = "Model";
    public const string SerialNumber = "SerialNumber";
    public const string OpenPorts = "OpenPorts";
    public const string SysDescr = "SysDescr";
    public const string SysName = "SysName";
    public const string ScanType = "ScanType";
    public const string RangeName = "RangeName";

    public static readonly string[] All = [
        IpAddress, MacAddress, Hostname, OsGuess, OsVersion,
        Vendor, Model, SerialNumber, OpenPorts, SysDescr, SysName,
        ScanType, RangeName
    ];
}

/// <summary>
/// Known operators for import rule criteria.
/// </summary>
public static class ImportRuleOperators
{
    public const string Contains = "Contains";
    public const string NotContains = "NotContains";
    public const string Equals = "Equals";
    public const string NotEquals = "NotEquals";
    public const string StartsWith = "StartsWith";
    public const string EndsWith = "EndsWith";
    public const string Matches = "Matches"; // Regex
    public const string GreaterThan = "GreaterThan";
    public const string LessThan = "LessThan";
    public const string IsEmpty = "IsEmpty";
    public const string IsNotEmpty = "IsNotEmpty";

    public static readonly string[] All = [
        Contains, NotContains, Equals, NotEquals, StartsWith, EndsWith,
        Matches, GreaterThan, LessThan, IsEmpty, IsNotEmpty
    ];
}

/// <summary>
/// Known action types for import rule actions.
/// </summary>
public static class ImportRuleActionTypes
{
    public const string AssignEntity = "AssignEntity";
    public const string AssignLocation = "AssignLocation";
    public const string AssignType = "AssignType";
    public const string AssignTag = "AssignTag";
    public const string CreateAsset = "CreateAsset";
    public const string UpdateAsset = "UpdateAsset";
    public const string Ignore = "Ignore";

    public static readonly string[] All = [
        AssignEntity, AssignLocation, AssignType, AssignTag,
        CreateAsset, UpdateAsset, Ignore
    ];
}
