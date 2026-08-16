using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(o => o.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(o => o.MessageId)
            .HasColumnName("message_id")
            .IsRequired();

        builder.Property(o => o.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(o => o.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(o => o.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("datetime(6)");

        builder.Property(o => o.NextRetryAt)
            .HasColumnName("next_retry_at")
            .HasColumnType("datetime(6)");

        builder.Property(o => o.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(2000);

        builder.HasIndex(o => new { o.Status, o.NextRetryAt });

        builder.HasIndex(o => o.TenantId);
    }
}
