using Daraban.Modules.Settings.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Settings.Data.Configurations;

/// <summary>Maps SystemSetting to core.system_settings (Task 7.4).</summary>
/// <remarks>
/// Lives in the cross-cutting <c>core</c> schema, next to audit_logs: these are platform
/// configuration values consumed by more than one module (email drives notifications,
/// security drives the login flow), not the private data of any single domain.
/// </remarks>
public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings", "core");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.Key)
            .HasColumnName("key")
            .HasMaxLength(100)
            .IsRequired();

        // text (not varchar): values are short, but a wrong cap must never silently
        // truncate what an admin typed -- better an oversized value than a broken one.
        builder.Property(s => s.Value).HasColumnName("value").IsRequired();

        builder.Property(s => s.ValueType)
            .HasColumnName("value_type")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Category)
            .HasColumnName("category")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(s => s.IsSecret).HasColumnName("is_secret").IsRequired();

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(s => s.CreatedById).HasColumnName("created_by_id");
        builder.Property(s => s.UpdatedById).HasColumnName("updated_by_id");

        builder.HasIndex(s => s.Key)
            .IsUnique()
            .HasDatabaseName("uq_system_settings_key");

        builder.HasIndex(s => s.Category).HasDatabaseName("ix_system_settings_category");
    }
}
