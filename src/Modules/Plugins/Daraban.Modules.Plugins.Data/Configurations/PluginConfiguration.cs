using Daraban.Modules.Plugins.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Plugins.Data.Configurations;

/// <summary>Maps Plugin to core.plugins (Task 7.5: "Plugin entity in core.* schema").</summary>
public class PluginConfiguration : IEntityTypeConfiguration<Plugin>
{
    public void Configure(EntityTypeBuilder<Plugin> builder)
    {
        builder.ToTable("plugins", "core");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.PluginId)
            .HasColumnName("plugin_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Version)
            .HasColumnName("version")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(p => p.Type)
            .HasColumnName("type")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.InstalledAt).HasColumnName("installed_at").IsRequired();

        // Manifest kept verbatim: text (not varchar) so no cap can silently truncate it.
        builder.Property(p => p.ManifestJson).HasColumnName("manifest_json").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(p => p.CreatedById).HasColumnName("created_by_id");
        builder.Property(p => p.UpdatedById).HasColumnName("updated_by_id");

        // Uninstalled rows are kept as tombstones, so uniqueness must hold across the
        // whole history of a plugin id -- a partial index would allow reinstall loops.
        builder.HasIndex(p => p.PluginId)
            .IsUnique()
            .HasDatabaseName("uq_core_plugins_plugin_id");

        builder.HasIndex(p => p.Status).HasDatabaseName("ix_core_plugins_status");
    }
}
