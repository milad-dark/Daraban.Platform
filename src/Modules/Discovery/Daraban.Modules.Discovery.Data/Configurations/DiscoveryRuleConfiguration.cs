using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Discovery.Data.Configurations;

/// <summary>EF configuration for DiscoveryRule entity (Task 5.1).</summary>
public class DiscoveryRuleConfiguration : IEntityTypeConfiguration<DiscoveryRule>
{
    public void Configure(EntityTypeBuilder<DiscoveryRule> builder)
    {
        builder.ToTable("DiscoveryRules");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasMaxLength(2000);

        builder.Property(r => r.FilterCriteria)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(r => r.Action)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.AssetType)
            .HasMaxLength(100);

        builder.Property(r => r.Tag)
            .HasMaxLength(100);

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(r => r.Name);
        builder.HasIndex(r => r.IsActive);
        builder.HasIndex(r => new { r.IsActive, r.Priority }); // For rule matching
    }
}
