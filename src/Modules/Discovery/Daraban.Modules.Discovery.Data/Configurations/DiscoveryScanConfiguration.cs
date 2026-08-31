using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Discovery.Data.Configurations;

/// <summary>EF configuration for DiscoveryScan entity (Task 5.1).</summary>
public class DiscoveryScanConfiguration : IEntityTypeConfiguration<DiscoveryScan>
{
    public void Configure(EntityTypeBuilder<DiscoveryScan> builder)
    {
        builder.ToTable("DiscoveryScans");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.ScanType)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(s => s.ErrorMessage)
            .HasMaxLength(4000);

        builder.Property(s => s.ScanLog)
            .HasColumnType("text");

        builder.Property(s => s.InitiatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(s => s.RangeId);
        builder.HasIndex(s => s.Status);
        builder.HasIndex(s => new { s.Status, s.QueuedAt }); // For queue processing
        builder.HasIndex(s => s.QueuedAt); // For history queries

        // Foreign key to DiscoveryRange
        builder.HasOne(s => s.Range)
            .WithMany(r => r.Scans)
            .HasForeignKey(s => s.RangeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
