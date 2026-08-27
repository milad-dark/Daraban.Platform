using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

public class AgentAuditLogConfiguration : IEntityTypeConfiguration<AgentAuditLog>
{
    public void Configure(EntityTypeBuilder<AgentAuditLog> b)
    {
        b.ToTable("agent_audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Action).HasMaxLength(128).IsRequired();
        b.Property(x => x.Detail).HasMaxLength(1024);
        b.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 max
        b.Property(x => x.UserAgent).HasMaxLength(512);
        b.Property(x => x.ErrorMessage).HasMaxLength(2000);
        b.Property(x => x.CorrelationId).HasMaxLength(64);
        b.Property(x => x.Metadata).HasMaxLength(4000);
        b.HasIndex(x => x.AgentId);
        b.HasIndex(x => x.Timestamp);
        b.HasIndex(x => new { x.AgentId, x.Timestamp });
        b.HasIndex(x => x.CorrelationId);
    }
}
