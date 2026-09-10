using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

/// <summary>
/// Append-only table (Task 7.3): no UPDATE/DELETE would ever be issued by application
/// code, but PostgreSQL-side guards keep that true even if a future bug tries.
/// Indexes mirror the two query paths: the paged browser (entity/actor/date filters)
/// and the per-record history panel.
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();

        b.Property(x => x.EntityType).HasMaxLength(128).IsRequired();
        b.Property(x => x.Action).HasMaxLength(16).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 max length
        b.Property(x => x.UserAgent).HasMaxLength(512);
        // old_values / new_values stay uncapped JSONB columns -- entity snapshots must
        // not be silently truncated, which would corrupt the diff trail.

        // Query paths: (entity, id) for the inline history panel; actor and date for
        // the paged browser filters. A covering (EntityType, EntityId, OccurredAt DESC)
        // index keeps the history panel ordered without a sort.
        b.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt });
        b.HasIndex(x => x.ActorUserId);
        b.HasIndex(x => x.OccurredAt);
    }
}
