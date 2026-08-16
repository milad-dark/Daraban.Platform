using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

public class ProfileRightConfiguration : IEntityTypeConfiguration<ProfileRight>
{
    public void Configure(EntityTypeBuilder<ProfileRight> b)
    {
        b.ToTable("profile_rights");
        b.HasKey(x => x.Id);
        b.Property(x => x.Module).HasMaxLength(64).IsRequired();
        b.Property(x => x.Action).HasMaxLength(128).IsRequired();
        b.HasIndex(x => new { x.ProfileId, x.Module, x.Action }).IsUnique();
        b.HasOne<Profile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
    }
}
