using Daraban.Modules.Reporting.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Reporting.Data.Configurations;

/// <summary>Maps ReportDefinition to reporting.report_definition (Task 7.2).</summary>
public class ReportDefinitionConfiguration : IEntityTypeConfiguration<ReportDefinition>
{
    public void Configure(EntityTypeBuilder<ReportDefinition> builder)
    {
        builder.ToTable("report_definition", "reporting");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");

        builder.Property(d => d.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Module).HasColumnName("module").HasMaxLength(100).IsRequired();
        builder.Property(d => d.FiltersJson).HasColumnName("filters_json").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.ColumnsJson).HasColumnName("columns_json").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.Format).HasColumnName("format").HasMaxLength(10).IsRequired();
        builder.Property(d => d.Schedule).HasColumnName("schedule").HasMaxLength(100);
        builder.Property(d => d.ScheduleEnabled).HasColumnName("schedule_enabled").IsRequired();
        builder.Property(d => d.OwnerUserId).HasColumnName("owner_user_id");
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(d => d.CreatedById).HasColumnName("created_by_id");
        builder.Property(d => d.UpdatedById).HasColumnName("updated_by_id");
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");

        builder.HasIndex(d => new { d.EntityId, d.Name })
            .IsUnique()
            .HasDatabaseName("uq_reporting_definition_entity_name")
            .HasFilter("is_deleted = false");

        builder.HasIndex(d => d.ScheduleEnabled)
            .HasDatabaseName("ix_reporting_definition_schedule_enabled")
            .HasFilter("schedule_enabled = true AND schedule IS NOT NULL");
    }
}

/// <summary>Maps SavedReport to reporting.saved_report (Task 7.2).</summary>
public class SavedReportConfiguration : IEntityTypeConfiguration<SavedReport>
{
    public void Configure(EntityTypeBuilder<SavedReport> builder)
    {
        builder.ToTable("saved_report", "reporting");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.DefinitionId).HasColumnName("definition_id").IsRequired();
        builder.Property(r => r.EntityId).HasColumnName("entity_id").IsRequired();
        builder.Property(r => r.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(r => r.Trigger).HasColumnName("trigger").HasMaxLength(20).IsRequired();
        builder.Property(r => r.Format).HasColumnName("format").HasMaxLength(10).IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").IsRequired();
        builder.Property(r => r.StorageReference).HasColumnName("storage_reference").HasMaxLength(500);
        builder.Property(r => r.StorageKind).HasColumnName("storage_kind").IsRequired();
        builder.Property(r => r.RowCount).HasColumnName("row_count");
        builder.Property(r => r.FileSizeBytes).HasColumnName("file_size_bytes");
        builder.Property(r => r.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);
        builder.Property(r => r.GeneratedAt).HasColumnName("generated_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.CreatedById).HasColumnName("created_by_id");
        builder.Property(r => r.UpdatedById).HasColumnName("updated_by_id");

        // The download path is always definition + report id -- no list-by-definition hot path
        // yet, but keep definition lookups indexed for the schedule history query.
        builder.HasIndex(r => r.DefinitionId).HasDatabaseName("ix_reporting_saved_report_definition");
    }
}
