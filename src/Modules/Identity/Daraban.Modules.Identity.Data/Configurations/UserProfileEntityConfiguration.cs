using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

public class UserProfileEntityConfiguration : IEntityTypeConfiguration<UserProfileEntity>
{
    public void Configure(EntityTypeBuilder<UserProfileEntity> b)
    {
        b.ToTable("user_profile_entities");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.ProfileId, x.EntityId }).IsUnique();
        // Both directions matter: "what can this user do" (permission resolution, keyed by
        // UserId) and "who has rights in this entity" (admin UI, keyed by EntityId).
        b.HasIndex(x => x.UserId);
        b.HasIndex(x => x.EntityId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Profile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<EntityNode>().WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade);
    }
}
