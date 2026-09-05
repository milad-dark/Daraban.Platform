using Daraban.Modules.Knowledge.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Knowledge.Data;

/// <summary>Owns the "knowledge" PostgreSQL schema (Task 1.2). Each module owns its own
/// DbContext/migrations -- no shared god-context across modules.</summary>
public class KnowledgeDbContext : DbContext
{
    public KnowledgeDbContext(DbContextOptions<KnowledgeDbContext> options) : base(options) { }

    // ---- Knowledge Base (Task 6.4) ----
    public DbSet<KbCategory> Categories => Set<KbCategory>();
    public DbSet<KbArticle> Articles => Set<KbArticle>();
    public DbSet<KbArticleTarget> ArticleTargets => Set<KbArticleTarget>();
    public DbSet<KbFeedback> Feedback => Set<KbFeedback>();
    public DbSet<KbTicketLink> TicketLinks => Set<KbTicketLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("knowledge");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KnowledgeDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
