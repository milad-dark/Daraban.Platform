using Daraban.Modules.Discovery.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Discovery.Data.Configurations;

/// <summary>EF configuration for SnmpCredential entity (Task 5.1).</summary>
public class SnmpCredentialConfiguration : IEntityTypeConfiguration<SnmpCredential>
{
    public void Configure(EntityTypeBuilder<SnmpCredential> builder)
    {
        builder.ToTable("SnmpCredentials");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Version)
            .HasConversion<string>()
            .HasMaxLength(10);

        // Community string (v1/v2c) - encrypted at rest
        builder.Property(c => c.CommunityString)
            .HasMaxLength(200);

        builder.Property(c => c.UserName)
            .HasMaxLength(100);

        builder.Property(c => c.AuthProtocol)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Auth passphrase - encrypted at rest
        builder.Property(c => c.AuthPassphrase)
            .HasMaxLength(500);

        builder.Property(c => c.PrivProtocol)
            .HasConversion<string>()
            .HasMaxLength(20);

        // Priv passphrase - encrypted at rest
        builder.Property(c => c.PrivPassphrase)
            .HasMaxLength(500);

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(100);

        // Indexes
        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.IsActive);

        // Note: Encrypt sensitive fields at application level before saving
        // CommunityString, AuthPassphrase, PrivPassphrase should be encrypted
    }
}
