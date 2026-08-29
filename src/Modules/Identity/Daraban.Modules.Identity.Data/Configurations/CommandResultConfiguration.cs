using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

public class CommandResultConfiguration : IEntityTypeConfiguration<CommandResult>
{
    public void Configure(EntityTypeBuilder<CommandResult> builder)
    {
        builder.ToTable("command_results");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(r => r.CommandId).HasColumnName("command_id").IsRequired();
        builder.Property(r => r.AgentId).HasColumnName("agent_id").IsRequired();
        builder.Property(r => r.Success).HasColumnName("success");
        builder.Property(r => r.Output).HasColumnName("output").HasColumnType("text");
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
        builder.Property(r => r.ExitCode).HasColumnName("exit_code");
        builder.Property(r => r.ExecutionDurationMs).HasColumnName("execution_duration_ms");
        builder.Property(r => r.ReceivedAt).HasColumnName("received_at").IsRequired();

        // Lookup by command
        builder.HasIndex(r => r.CommandId)
            .HasDatabaseName("ix_command_results_command_id");

        // Lookup by agent + time for reporting
        builder.HasIndex(r => new { r.AgentId, r.ReceivedAt })
            .HasDatabaseName("ix_command_results_agent_received");
    }
}
