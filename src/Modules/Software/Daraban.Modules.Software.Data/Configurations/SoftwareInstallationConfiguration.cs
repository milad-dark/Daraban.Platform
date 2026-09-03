using Daraban.Modules.Software.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Software.Data.Configurations;

public class SoftwareInstallationConfiguration : IEntityTypeConfiguration<SoftwareInstallation>
{
    public void Configure(EntityTypeBuilder<SoftwareInstallation> builder)
    {
        builder.ToTable("software_installations");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.SoftwareId)
            .HasColumnName("software_id")
            .IsRequired();

        builder.Property(i => i.LicenseId)
            .HasColumnName("license_id");

        builder.Property(i => i.AssetId)
            .HasColumnName("asset_id")
            .IsRequired();

        builder.Property(i => i.InstalledVersion)
            .HasColumnName("installed_version")
            .HasMaxLength(50);

        builder.Property(i => i.InstalledDate)
            .HasColumnName("installed_date")
            .IsRequired();

        builder.Property(i => i.UninstalledDate)
            .HasColumnName("uninstalled_date");

        builder.Property(i => i.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(i => i.InstallPath)
            .HasColumnName("install_path")
            .HasMaxLength(500);

        builder.Property(i => i.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(i => i.Comment)
            .HasColumnName("comment")
            .HasMaxLength(1000);

        builder.Property(i => i.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(i => i.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(i => i.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(i => i.DeletedAt)
            .HasColumnName("deleted_at");

        // Relationships
        builder.HasOne(i => i.Software)
            .WithMany(s => s.Installations)
            .HasForeignKey(i => i.SoftwareId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.License)
            .WithMany(l => l.Installations)
            .HasForeignKey(i => i.LicenseId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(i => i.SoftwareId)
            .HasDatabaseName("ix_software_installations_software_id");

        builder.HasIndex(i => i.LicenseId)
            .HasDatabaseName("ix_software_installations_license_id");

        builder.HasIndex(i => i.AssetId)
            .HasDatabaseName("ix_software_installations_asset_id");

        builder.HasIndex(i => new { i.AssetId, i.SoftwareId })
            .HasDatabaseName("ix_software_installations_asset_software");

        builder.HasIndex(i => i.IsActive)
            .HasDatabaseName("ix_software_installations_is_active");
    }
}
