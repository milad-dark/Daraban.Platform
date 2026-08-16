using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Daraban.Modules.Identity.Data;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<EntityNode> Entities => Set<EntityNode>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<ProfileRight> ProfileRights => Set<ProfileRight>();
    public DbSet<UserProfileEntity> UserProfileEntities => Set<UserProfileEntity>();
    // Groups follows the identical shape -- add alongside as it's built out (Task 1.2 SS3).

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
