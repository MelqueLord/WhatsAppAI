using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Integrations;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppWebSessionLeaseConfiguration : IEntityTypeConfiguration<WhatsAppWebSessionLease>
{
    public void Configure(EntityTypeBuilder<WhatsAppWebSessionLease> builder)
    {
        builder.ToTable("whatsapp_web_session_leases");

        builder.HasKey(lease => lease.SessionId);

        builder.Property(lease => lease.SessionId)
            .HasColumnName("session_id")
            .HasMaxLength(64);

        builder.Property(lease => lease.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(lease => lease.LineNumber)
            .HasColumnName("line_number")
            .IsRequired();

        builder.Property(lease => lease.OwnerInstanceId)
            .HasColumnName("owner_instance_id")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(lease => lease.OwnerBaseUrl)
            .HasColumnName("owner_base_url")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(lease => lease.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(lease => lease.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(lease => lease.ExpiresAt);
        builder.HasIndex(lease => new { lease.TenantId, lease.LineNumber }).IsUnique();
    }
}
