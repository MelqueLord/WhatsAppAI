using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(m => m.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(m => m.ContactId)
            .HasColumnName("contact_id")
            .IsRequired();

        builder.Property(m => m.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(100);

        builder.Property(m => m.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .HasMaxLength(4000);

        builder.Property(m => m.MediaId)
            .HasColumnName("media_id")
            .HasMaxLength(200);

        builder.Property(m => m.MediaUrl)
            .HasColumnName("media_url")
            .HasMaxLength(500);

        builder.Property(m => m.Caption)
            .HasColumnName("caption")
            .HasMaxLength(4000);

        builder.Property(m => m.QuotedMessageId)
            .HasColumnName("quoted_message_id")
            .HasMaxLength(100);

        builder.Property(m => m.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(200);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(m => m.SentAt)
            .HasColumnName("sent_at")
            .HasColumnType("datetime(6)");

        builder.Property(m => m.DeliveredAt)
            .HasColumnName("delivered_at")
            .HasColumnType("datetime(6)");

        builder.Property(m => m.ReadAt)
            .HasColumnName("read_at")
            .HasColumnType("datetime(6)");

        builder.Property(m => m.FailedAt)
            .HasColumnName("failed_at")
            .HasColumnType("datetime(6)");

        builder.Property(m => m.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(2000);

        builder.Property(m => m.ProcessedByAi)
            .HasColumnName("processed_by_ai")
            .IsRequired();

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Contact)
            .WithMany()
            .HasForeignKey(m => m.ContactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => m.ExternalId);

        builder.HasIndex(m => m.IdempotencyKey);

        builder.HasIndex(m => new { m.TenantId, m.ConversationId, m.CreatedAt });

        builder.HasIndex(m => m.TenantId);
    }
}
