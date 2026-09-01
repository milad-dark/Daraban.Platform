using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Discovery.Data.Configurations;

/// <summary>EF configuration for DiscoveryRange entity (Task 5.1).</summary>
public class DiscoveryRangeConfiguration : IEntityTypeConfiguration<DiscoveryRange>
{
    public void Configure(EntityTypeBuilder<DiscoveryRange> builder)
    {
        builder.ToTable("DiscoveryRanges");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.CidrRange)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.StartIp)
            .HasMaxLength(45); // IPv6 max length

        builder.Property(r => r.EndIp)
            .HasMaxLength(45);

        builder.Property(r => r.ScanType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(r => r.CreatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(r => r.Name);
        builder.HasIndex(r => r.IsActive);
        builder.HasIndex(r => r.CidrRange);
        builder.HasIndex(r => new { r.IsActive, r.ScanIntervalHours })
            .HasFilter("\"IsActive\" = true"); // For scheduled scan queries

        // Foreign key to SnmpCredential
        builder.HasOne(r => r.SnmpCredential)
            .WithMany(c => c.DiscoveryRanges)
            .HasForeignKey(r => r.SnmpCredentialId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
