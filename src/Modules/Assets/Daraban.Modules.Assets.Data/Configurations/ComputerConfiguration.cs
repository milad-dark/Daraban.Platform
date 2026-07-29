using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Assets.Data.Configurations;

public class ComputerConfiguration : IEntityTypeConfiguration<Computer>
{
    public void Configure(EntityTypeBuilder<Computer> b)
    {
        b.ToTable("computers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(256).IsRequired();
        b.HasIndex(x => new { x.EntityId, x.SerialNumber }).IsUnique();
        b.HasIndex(x => x.EntityId);
        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
