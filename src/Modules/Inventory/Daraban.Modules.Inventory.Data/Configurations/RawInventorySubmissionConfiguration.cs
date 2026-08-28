using Daraban.Modules.Inventory.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Inventory.Data.Configurations;

/// <summary>
/// EF Core configuration for RawInventorySubmission (Task 4.3).
/// Key indexes:
/// - SubmissionHash UNIQUE — enforces idempotency at the DB level
/// - AgentId + ReceivedAt — fast lookups for "submissions from agent X"
/// - Status + ReceivedAt — efficient background worker polling
/// </summary>
public class RawInventorySubmissionConfiguration : IEntityTypeConfiguration<RawInventorySubmission>
{
    public void Configure(EntityTypeBuilder<RawInventorySubmission> builder)
    {
        builder.ToTable("raw_inventory_submissions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.SubmissionHash)
            .IsRequired()
            .HasMaxLength(64); // SHA-256 hex = 64 chars

        builder.Property(x => x.DeviceId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.ItemType)
            .HasMaxLength(64);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.RawPayload)
            .IsRequired()
            .HasColumnType("text"); // no max length — inventory payloads vary widely

        builder.Property(x => x.FullEnvelope)
            .IsRequired()
            .HasColumnType("text"); // no max length — stores full envelope for audit

        builder.Property(x => x.ErrorMessage)
            .HasMaxLength(2048);

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45); // IPv6 max

        // Idempotency: prevent duplicate submissions within the same minute
        builder.HasIndex(x => x.SubmissionHash)
            .IsUnique();

        // Query: "list all submissions from agent X, newest first"
        builder.HasIndex(x => new { x.AgentId, x.ReceivedAt })
            .IsDescending(false, true);

        // Query: background worker polling for Pending submissions
        builder.HasIndex(x => new { x.Status, x.ReceivedAt })
            .IsDescending(false, true);

        // Query: look up submissions by device
        builder.HasIndex(x => new { x.DeviceId, x.ReceivedAt })
            .IsDescending(false, true);
    }
}
