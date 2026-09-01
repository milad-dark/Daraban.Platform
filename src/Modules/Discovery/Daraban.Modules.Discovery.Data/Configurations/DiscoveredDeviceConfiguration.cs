using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Discovery.Data.Configurations;

/// <summary>EF configuration for DiscoveredDevice entity (Task 5.1).</summary>
public class DiscoveredDeviceConfiguration : IEntityTypeConfiguration<DiscoveredDevice>
{
    public void Configure(EntityTypeBuilder<DiscoveredDevice> builder)
    {
        builder.ToTable("DiscoveredDevices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.IpAddress)
            .IsRequired()
            .HasMaxLength(45); // IPv6 max length

        builder.Property(d => d.MacAddress)
            .HasMaxLength(17); // XX:XX:XX:XX:XX:XX

        builder.Property(d => d.Hostname)
            .HasMaxLength(255);

        builder.Property(d => d.OsGuess)
            .HasMaxLength(200);

        builder.Property(d => d.OsVersion)
            .HasMaxLength(200);

        builder.Property(d => d.Vendor)
            .HasMaxLength(200);

        builder.Property(d => d.Model)
            .HasMaxLength(200);

        builder.Property(d => d.SerialNumber)
            .HasMaxLength(100);

        builder.Property(d => d.OpenPorts)
            .HasColumnType("text");

        builder.Property(d => d.SysDescr)
            .HasMaxLength(1000);

        builder.Property(d => d.SysName)
            .HasMaxLength(255);

        builder.Property(d => d.SysLocation)
            .HasMaxLength(500);

        builder.Property(d => d.SysContact)
            .HasMaxLength(500);

        // Indexes
        builder.HasIndex(d => d.ScanId);
        builder.HasIndex(d => d.RangeId);
        builder.HasIndex(d => d.IpAddress);
        builder.HasIndex(d => d.MacAddress);
        builder.HasIndex(d => d.Hostname);
        builder.HasIndex(d => new { d.RangeId, d.IpAddress }); // For upsert queries
        builder.HasIndex(d => d.DiscoveredAt);
        builder.HasIndex(d => d.LastSeenAt);

        // Foreign keys
        builder.HasOne(d => d.Scan)
            .WithMany(s => s.Devices)
            .HasForeignKey(d => d.ScanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Range)
            .WithMany()
            .HasForeignKey(d => d.RangeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
