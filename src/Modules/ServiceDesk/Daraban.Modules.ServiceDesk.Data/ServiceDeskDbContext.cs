using Daraban.Modules.ServiceDesk.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.ServiceDesk.Data;

/// <summary>Owns the "servicedesk" PostgreSQL schema (Task 1.2). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class ServiceDeskDbContext : DbContext
{
    public ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : base(options) { }

    // Ticket Management
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketTask> TicketTasks => Set<TicketTask>();
    public DbSet<TicketTemplate> TicketTemplates => Set<TicketTemplate>();
    public DbSet<TicketValidation> TicketValidations => Set<TicketValidation>();
    public DbSet<TicketCost> TicketCosts => Set<TicketCost>();
    public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("servicedesk");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServiceDeskDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
