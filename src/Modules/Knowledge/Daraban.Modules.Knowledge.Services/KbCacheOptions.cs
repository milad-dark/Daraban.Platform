namespace Daraban.Modules.Knowledge.Services;

/// <summary>
/// Task 8.2: tunables for the KB read-model cache. Defaults match what existed before this
/// options type (60 s), so an environment that configures nothing keeps the same behaviour.
/// The cache is deliberately short-lived: KB lists and search results carry counters
/// (views/helpfulness) that drift between writes, and a wrong entry is corrected by expiry
/// even if an invalidation path is ever missed.
/// </summary>
public sealed class KbCacheOptions
{
    public const string SectionName = "KbCache";

    /// <summary>Sliding window for cached paged/search results.</summary>
    public int TtlSeconds { get; set; } = 60;

    /// <summary>Hard cap on cached result entries; oldest-evicted-first beyond it.</summary>
    public int SizeLimit { get; set; } = 1_024;
}
