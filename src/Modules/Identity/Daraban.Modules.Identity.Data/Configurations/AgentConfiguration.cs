using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

public class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> b)
    {
        b.ToTable("agents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.AllowedScopes).HasMaxLength(2000).IsRequired();
        b.Property(x => x.Tags).HasMaxLength(4000);
        b.HasIndex(x => x.Name);
        b.HasIndex(x => x.OwnerUserId);
        b.HasIndex(x => x.EntityId);
        b.HasIndex(x => x.Status);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
