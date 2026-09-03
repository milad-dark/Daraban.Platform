using Daraban.Modules.Financial.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Financial.Data;

/// <summary>Owns the "financial" PostgreSQL schema (Task 1.2). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class FinancialDbContext : DbContext
{
    public FinancialDbContext(DbContextOptions<FinancialDbContext> options) : base(options) { }

    // Financial entities
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractType> ContractTypes => Set<ContractType>();
    public DbSet<ContractAsset> ContractAssets => Set<ContractAsset>();
    public DbSet<ContractCost> ContractCosts => Set<ContractCost>();
    public DbSet<Infocom> Infocoms => Set<Infocom>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("financial");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinancialDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
