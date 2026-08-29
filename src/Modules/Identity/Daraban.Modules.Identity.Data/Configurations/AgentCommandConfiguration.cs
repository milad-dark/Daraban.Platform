using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

public class AgentCommandConfiguration : IEntityTypeConfiguration<AgentCommand>
{
    public void Configure(EntityTypeBuilder<AgentCommand> builder)
    {
        builder.ToTable("agent_commands");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.AgentId).HasColumnName("agent_id").IsRequired();
        builder.Property(c => c.CommandType).HasColumnName("command_type").HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.Payload).HasColumnName("payload").HasColumnType("text");
        builder.Property(c => c.TimeoutSeconds).HasColumnName("timeout_seconds");
        builder.Property(c => c.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
        builder.Property(c => c.MaxRetries).HasColumnName("max_retries").HasDefaultValue(0);
        builder.Property(c => c.LastError).HasColumnName("last_error").HasColumnType("text");
        builder.Property(c => c.ExitCode).HasColumnName("exit_code");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(c => c.DispatchedAt).HasColumnName("dispatched_at");
        builder.Property(c => c.ReceivedAt).HasColumnName("received_at");
        builder.Property(c => c.CompletedAt).HasColumnName("completed_at");
        builder.Property(c => c.DeadlineAt).HasColumnName("deadline_at");

        // Index for polling pending commands: agent + status = 'Queued' or 'Created'
        builder.HasIndex(c => new { c.AgentId, c.Status })
            .HasDatabaseName("ix_agent_commands_agent_status");

        // Index for timeout checker: status + deadline
        builder.HasIndex(c => new { c.Status, c.DeadlineAt })
            .HasDatabaseName("ix_agent_commands_status_deadline");

        // Index for admin listing by creation time
        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("ix_agent_commands_created_at");
    }
}
