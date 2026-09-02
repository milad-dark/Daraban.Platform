using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Discovery.Data;

/// <summary>Owns the "discovery" PostgreSQL schema (Task 5.1). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class DiscoveryDbContext(DbContextOptions<DiscoveryDbContext> options) : DbContext(options)
{
    public DbSet<DiscoveryRange> DiscoveryRanges => Set<DiscoveryRange>();
    public DbSet<DiscoveryScan> DiscoveryScans => Set<DiscoveryScan>();
    public DbSet<DiscoveredDevice> DiscoveredDevices => Set<DiscoveredDevice>();
    public DbSet<SnmpCredential> SnmpCredentials => Set<SnmpCredential>();
    public DbSet<DiscoveryRule> DiscoveryRules => Set<DiscoveryRule>();
    public DbSet<ImportRule> ImportRules => Set<ImportRule>();
    public DbSet<ImportRuleCriteria> ImportRuleCriteria => Set<ImportRuleCriteria>();
    public DbSet<ImportRuleAction> ImportRuleActions => Set<ImportRuleAction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("discovery");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DiscoveryDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
