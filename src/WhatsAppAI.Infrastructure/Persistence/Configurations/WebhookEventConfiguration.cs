using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_events");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(e => e.PhoneNumberId)
            .HasColumnName("phone_number_id")
            .HasMaxLength(100);

        builder.Property(e => e.RoutingKind)
            .HasColumnName("routing_kind")
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(WebhookRoutingKind.PhoneNumber)
            .IsRequired();

        builder.Property(e => e.RoutingId)
            .HasColumnName("routing_id")
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(e => e.EventKind)
            .HasColumnName("event_kind")
            .HasMaxLength(100)
            .HasDefaultValue(string.Empty)
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(e => e.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.EncryptedPayload)
            .HasColumnName("encrypted_payload")
            .HasMaxLength(100000)
            .IsRequired();

        builder.Property(e => e.Signature)
            .HasColumnName("signature")
            .HasMaxLength(200);

        builder.Property(e => e.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(2000);

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(e => e.ProcessedAt)
            .HasColumnName("processed_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(e => e.NextRetryAt)
            .HasColumnName("next_retry_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(e => e.IdempotencyKey)
            .IsUnique();

        builder.HasIndex(e => e.Status);

        builder.HasIndex(e => e.NextRetryAt);

        builder.HasIndex(e => new { e.Status, e.CreatedAt });
        builder.HasIndex(e => new { e.RoutingKind, e.RoutingId, e.Status });
    }
}
