using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Audit;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.UserId).HasColumnName("user_id");
        builder.Property(a => a.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(100);
        builder.Property(a => a.Details).HasColumnName("details").HasMaxLength(2000);
        builder.Property(a => a.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
        builder.Property(a => a.OccurredAt).HasColumnName("occurred_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.OccurredAt });
        builder.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId });

        // Immutable: no UPDATE/DELETE at DB level via EF
        builder.HasQueryFilter(null); // Override default filters for audit
    }
}
