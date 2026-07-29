using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Identity.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    // Entities, Groups, Profiles, ProfileRights, UserProfileEntities, RefreshTokens
    // follow the identical shape -- omitted here to keep this scaffold readable; add
    // alongside User as each is built out (Task 1.2 SS3).

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
