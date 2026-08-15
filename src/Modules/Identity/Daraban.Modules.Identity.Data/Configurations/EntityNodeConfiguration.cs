using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

public class EntityNodeConfiguration : IEntityTypeConfiguration<EntityNode>
{
    public void Configure(EntityTypeBuilder<EntityNode> b)
    {
        b.ToTable("entities");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.Property(x => x.FullPath).HasMaxLength(2048).IsRequired();
        b.HasIndex(x => x.FullPath); // backs the prefix-match recursive-scope query, Task 1.2 SS9
        b.HasIndex(x => x.ParentId);
        b.HasOne<EntityNode>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
