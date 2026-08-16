using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Messaging;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class HandoffEventConfiguration : IEntityTypeConfiguration<HandoffEvent>
{
    public void Configure(EntityTypeBuilder<HandoffEvent> builder)
    {
        builder.ToTable("handoff_events");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(h => h.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(h => h.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(h => h.FromMode)
            .HasColumnName("from_mode")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(h => h.ToMode)
            .HasColumnName("to_mode")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(h => h.OperatorUserId)
            .HasColumnName("operator_user_id");

        builder.Property(h => h.Reason)
            .HasColumnName("reason")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(h => h.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.HasIndex(h => new { h.TenantId, h.ConversationId, h.OccurredAt });
    }
}
