using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppAI.Domain.Automation;

namespace WhatsAppAI.Infrastructure.Persistence.Configurations;

public sealed class AiInteractionConfiguration : IEntityTypeConfiguration<AiInteraction>
{
    public void Configure(EntityTypeBuilder<AiInteraction> builder)
    {
        builder.ToTable("ai_interactions");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(i => i.ConversationId).HasColumnName("conversation_id").IsRequired();
        builder.Property(i => i.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(i => i.ResponseMessageId).HasColumnName("response_message_id");
        builder.Property(i => i.ModelId).HasColumnName("model_id").HasMaxLength(100).IsRequired();
        builder.Property(i => i.Decision).HasColumnName("decision").HasMaxLength(20).IsRequired();
        builder.Property(i => i.HandoffReason).HasColumnName("handoff_reason").HasMaxLength(500);
        builder.Property(i => i.Confidence).HasColumnName("confidence").IsRequired();
        builder.Property(i => i.InputTokens).HasColumnName("input_tokens").IsRequired();
        builder.Property(i => i.OutputTokens).HasColumnName("output_tokens").IsRequired();
        builder.Property(i => i.LatencyMs).HasColumnName("latency_ms").IsRequired();
        builder.Property(i => i.ResponseId).HasColumnName("response_id").HasMaxLength(100);
        builder.Property(i => i.FeedbackRating).HasColumnName("feedback_rating").HasConversion<string>().HasMaxLength(32);
        builder.Property(i => i.FeedbackNote).HasColumnName("feedback_note").HasMaxLength(1000);
        builder.Property(i => i.CorrectedResponse).HasColumnName("corrected_response").HasMaxLength(160);
        builder.Property(i => i.FeedbackByUserId).HasColumnName("feedback_by_user_id");
        builder.Property(i => i.FeedbackAt).HasColumnName("feedback_at").HasColumnType("timestamp with time zone");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").IsRequired();

        builder.HasIndex(i => new { i.TenantId, i.ConversationId, i.CreatedAt });
        builder.HasIndex(i => i.TenantId);
        builder.HasIndex(i => new { i.TenantId, i.ResponseMessageId }).IsUnique();
    }
}
