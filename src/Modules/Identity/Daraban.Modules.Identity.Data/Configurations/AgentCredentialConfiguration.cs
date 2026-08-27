using Daraban.Modules.Identity.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Daraban.Modules.Identity.Data.Configurations;

public class AgentCredentialConfiguration : IEntityTypeConfiguration<AgentCredential>
{
    public void Configure(EntityTypeBuilder<AgentCredential> b)
    {
        b.ToTable("agent_credentials");
        b.HasKey(x => x.Id);
        b.Property(x => x.ClientId).HasMaxLength(128).IsRequired();
        b.Property(x => x.ClientSecretHash).HasMaxLength(512).IsRequired();
        b.Property(x => x.Label).HasMaxLength(256);
        b.Property(x => x.Scopes).HasMaxLength(2000);
        b.HasIndex(x => x.ClientId).IsUnique();
        b.HasIndex(x => x.AgentId);
    }
}
