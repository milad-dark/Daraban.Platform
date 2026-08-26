using Daraban.Modules.Assets.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Daraban.Modules.Assets.Data.Configurations;

public class ManufacturerConfiguration : IEntityTypeConfiguration<Manufacturer>
{
    public void Configure(EntityTypeBuilder<Manufacturer> builder)
    {
        builder.ToTable("manufacturers", "assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Website).HasMaxLength(500);
        builder.Property(x => x.SupportUrl).HasMaxLength(500);
        builder.Property(x => x.SupportPhone).HasMaxLength(50);
        builder.HasQueryFilter(x => x.DeletedAt == null);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}
