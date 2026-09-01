namespace Daraban.Modules.Discovery.Data.Entities;

/// <summary>
/// SNMP credentials for device discovery (Task 5.1).
/// Supports SNMPv1/v2c (community string) and SNMPv3 (user/auth/priv).
/// </summary>
public class SnmpCredential
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name (e.g., "Main Office SNMPv3").</summary>
    public string Name { get; set; } = default!;

    /// <summary>SNMP version.</summary>
    public SnmpVersion Version { get; set; } = SnmpVersion.V2c;

    /// <summary>Community string (v1/v2c only, encrypted at rest).</summary>
    public string? CommunityString { get; set; }

    /// <summary>SNMPv3 username.</summary>
    public string? UserName { get; set; }

    /// <summary>SNMPv3 authentication protocol (None, MD5, SHA, SHA224, SHA256, SHA384, SHA512).</summary>
    public AuthProtocol AuthProtocol { get; set; } = AuthProtocol.None;

    /// <summary>SNMPv3 authentication passphrase (encrypted at rest).</summary>
    public string? AuthPassphrase { get; set; }

    /// <summary>SNMPv3 privacy protocol (None, DES, AES128, AES192, AES256).</summary>
    public PrivProtocol PrivProtocol { get; set; } = PrivProtocol.None;

    /// <summary>SNMPv3 privacy passphrase (encrypted at rest).</summary>
    public string? PrivPassphrase { get; set; }

    /// <summary>Whether this credential is active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When this credential was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When this credential was last modified.</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>User who created this credential.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Navigation: discovery ranges using this credential.</summary>
    public ICollection<DiscoveryRange> DiscoveryRanges { get; set; } = new List<DiscoveryRange>();
}

/// <summary>SNMP version enumeration.</summary>
public enum SnmpVersion
{
    /// <summary>SNMPv1 (legacy, not recommended).</summary>
    V1 = 1,

    /// <summary>SNMPv2c (community string based).</summary>
    V2c = 2,

    /// <summary>SNMPv3 (user-based authentication).</summary>
    V3 = 3,
}

/// <summary>SNMPv3 authentication protocol.</summary>
public enum AuthProtocol
{
    None = 0,
    MD5 = 1,
    SHA = 2,
    SHA224 = 3,
    SHA256 = 4,
    SHA384 = 5,
    SHA512 = 6,
}

/// <summary>SNMPv3 privacy protocol.</summary>
public enum PrivProtocol
{
    None = 0,
    DES = 1,
    AES128 = 2,
    AES192 = 3,
    AES256 = 4,
}
