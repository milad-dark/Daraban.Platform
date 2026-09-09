using Daraban.Modules.Dashboard.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Dashboard.Data.Configurations;

/// <summary>Maps DashboardLayout to dashboard.layout (Task 7.1).</summary>
public class DashboardLayoutConfiguration : IEntityTypeConfiguration<DashboardLayout>
{
    public void Configure(EntityTypeBuilder<DashboardLayout> builder)
    {
        builder.ToTable("layout", "dashboard");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        // PostgreSQL jsonb -- the DB validates the JSON and allows querying later.
        builder.Property(l => l.LayoutJson)
            .HasColumnName("layout_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(l => l.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(l => l.CreatedById).HasColumnName("created_by_id");
        builder.Property(l => l.UpdatedById).HasColumnName("updated_by_id");

        // One default layout per user. Name is part of the key for future multi-page dashboards.
        builder.HasIndex(l => new { l.UserId, l.Name })
            .IsUnique()
            .HasDatabaseName("uq_dashboard_layout_user_name");

        builder.HasIndex(l => l.UserId).HasDatabaseName("ix_dashboard_layout_user_id");
    }
}
