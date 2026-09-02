using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Discovery.Data.Configurations;

/// <summary>
/// EF configuration for ImportRule entity.
/// </summary>
public class ImportRuleConfiguration : IEntityTypeConfiguration<ImportRule>
{
    public void Configure(EntityTypeBuilder<ImportRule> builder)
    {
        builder.ToTable("import_rules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(r => r.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(r => r.Priority).HasColumnName("priority").HasDefaultValue(0);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.ModifiedAt).HasColumnName("modified_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(100);

        // Indexes
        builder.HasIndex(r => r.Name).IsUnique();
        builder.HasIndex(r => r.IsActive);
        builder.HasIndex(r => r.Priority);
    }
}

/// <summary>
/// EF configuration for ImportRuleCriteria entity.
/// </summary>
public class ImportRuleCriteriaConfiguration : IEntityTypeConfiguration<ImportRuleCriteria>
{
    public void Configure(EntityTypeBuilder<ImportRuleCriteria> builder)
    {
        builder.ToTable("import_rule_criteria");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ImportRuleId).HasColumnName("import_rule_id").IsRequired();
        builder.Property(c => c.Field).HasColumnName("field").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Operator).HasColumnName("operator").HasMaxLength(50).IsRequired();
        builder.Property(c => c.Value).HasColumnName("value").HasMaxLength(500).IsRequired();

        // Foreign key
        builder.HasOne(c => c.ImportRule)
            .WithMany(r => r.Criteria)
            .HasForeignKey(c => c.ImportRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(c => c.ImportRuleId);
    }
}

/// <summary>
/// EF configuration for ImportRuleAction entity.
/// </summary>
public class ImportRuleActionConfiguration : IEntityTypeConfiguration<ImportRuleAction>
{
    public void Configure(EntityTypeBuilder<ImportRuleAction> builder)
    {
        builder.ToTable("import_rule_actions");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ImportRuleId).HasColumnName("import_rule_id").IsRequired();
        builder.Property(a => a.ActionType).HasColumnName("action_type").HasMaxLength(50).IsRequired();
        builder.Property(a => a.Value).HasColumnName("value").HasMaxLength(500);

        // Foreign key
        builder.HasOne(a => a.ImportRule)
            .WithMany(r => r.Actions)
            .HasForeignKey(a => a.ImportRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(a => a.ImportRuleId);
    }
}
